﻿using System;

namespace Serval.Client
{
    public class HttpException : Exception
    {
        public HttpException(string message)
            : base(message) { }

        public int StatusCode { get; set; }
    }
}
