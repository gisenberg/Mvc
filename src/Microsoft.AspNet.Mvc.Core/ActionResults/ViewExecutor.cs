﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.Framework.Internal;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNet.Mvc
{
    /// <summary>
    /// Utility type for rendering a <see cref="IView"/> to the response.
    /// </summary>
    public static class ViewExecutor
    {
        private const int BufferSize = 1024;
        private static readonly MediaTypeHeaderValue DefaultContentType = new MediaTypeHeaderValue("text/html")
        {
            Charset = Encodings.UTF8EncodingWithoutBOM.WebName
        };

        /// <summary>
        /// Asynchronously renders the specified <paramref name="view"/> to the response body.
        /// </summary>
        /// <param name="view">The <see cref="IView"/> to render.</param>
        /// <param name="actionContext">The <see cref="ActionContext"/> for the current executing action.</param>
        /// <param name="viewData">The <see cref="ViewDataDictionary"/> for the view being rendered.</param>
        /// <param name="tempData">The <see cref="ITempDataDictionary"/> for the view being rendered.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous rendering.</returns>
        public static async Task ExecuteAsync([NotNull] IView view,
                                              [NotNull] ActionContext actionContext,
                                              [NotNull] ViewDataDictionary viewData,
                                              [NotNull] ITempDataDictionary tempData,
                                              MediaTypeHeaderValue contentType)
        {
            var response = actionContext.HttpContext.Response;

            MediaTypeHeaderValue contentTypeHeader = contentType;
            Encoding encoding;
            if (contentTypeHeader == null)
            {
                contentTypeHeader = DefaultContentType;
                encoding = Encodings.UTF8EncodingWithoutBOM;
            }
            else
            {
                if (string.IsNullOrEmpty(contentTypeHeader.Charset))
                {
                    encoding = Encodings.UTF8EncodingWithoutBOM;

                    // 1. Do not modify the user supplied content type
                    // 2. Parse here to handle parameters apart from charset
                    contentTypeHeader = MediaTypeHeaderValue.Parse(contentTypeHeader.ToString());
                    contentTypeHeader.Charset = encoding.WebName;
                }
                else
                {
                    if(string.Equals(
                        contentTypeHeader.Charset,
                        Encodings.UTF8EncodingWithoutBOM.WebName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        encoding = Encodings.UTF8EncodingWithoutBOM;
                    }
                    else if(string.Equals(
                        contentTypeHeader.Charset,
                        Encodings.UTF16EncodingLittleEndian.WebName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        encoding = Encodings.UTF16EncodingLittleEndian;
                    }
                    else
                    {
                        encoding = Encoding.GetEncoding(contentTypeHeader.Charset);
                    }
                }
            }

            response.ContentType = contentTypeHeader.ToString();

            var wrappedStream = new StreamWrapper(response.Body);

            using (var writer = new StreamWriter(wrappedStream, encoding, BufferSize, leaveOpen: true))
            {
                try
                {
                    var viewContext = new ViewContext(actionContext, view, viewData, tempData, writer);
                    await view.RenderAsync(viewContext);
                }
                catch
                {
                    // Need to prevent writes/flushes on dispose because the StreamWriter will flush even if
                    // nothing got written. This leads to a response going out on the wire prematurely in case an
                    // exception is being thrown inside the try catch block.
                    wrappedStream.BlockWrites = true;
                    throw;
                }
            }
        }

        private class StreamWrapper : Stream
        {
            private readonly Stream _wrappedStream;

            public StreamWrapper(Stream stream)
            {
                _wrappedStream = stream;
            }

            public bool BlockWrites { get; set; }

            public override bool CanRead
            {
                get { return _wrappedStream.CanRead; }
            }

            public override bool CanSeek
            {
                get { return _wrappedStream.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return _wrappedStream.CanWrite; }
            }

            public override void Flush()
            {
                if (!BlockWrites)
                {
                    _wrappedStream.Flush();
                }
            }

            public override long Length
            {
                get { return _wrappedStream.Length; }
            }

            public override long Position
            {
                get
                {
                    return _wrappedStream.Position;
                }
                set
                {
                    _wrappedStream.Position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _wrappedStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (!BlockWrites)
                {
                    _wrappedStream.Write(buffer, offset, count);
                }
            }
        }
    }
}