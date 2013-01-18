﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using Segmentio.Model;

namespace Segmentio.Request
{
    internal class BatchState
    {
        internal HttpWebRequest Request { get; set; }
        internal Batch Batch { get; set; }

        internal BatchState(HttpWebRequest request, Batch batch)
        {
            this.Request = request;
            this.Batch = batch;
        }
    }
}
