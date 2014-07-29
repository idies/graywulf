﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.Net;
using System.ServiceModel.Dispatcher;
using Jhu.Graywulf.Components;

namespace Jhu.Graywulf.Web.Api
{
    class DynamicResponseMessageFormatter : IDispatchMessageFormatter
    {
        private IDispatchMessageFormatter fallbackFormatter;
        private Dictionary<string, IDispatchMessageFormatter> formatters;

        public Dictionary<string, IDispatchMessageFormatter> Formatters
        {
            get { return formatters; }
        }

        public DynamicResponseMessageFormatter(IDispatchMessageFormatter fallbackFormatter, IDictionary<string, IDispatchMessageFormatter> formatters)
        {
            this.fallbackFormatter = fallbackFormatter;
            this.formatters = new Dictionary<string, IDispatchMessageFormatter>(formatters, StringComparer.InvariantCultureIgnoreCase);
        }

        public void DeserializeRequest(Message message, object[] parameters)
        {
            fallbackFormatter.DeserializeRequest(message, parameters);
        }

        public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
        {
            var request = OperationContext.Current.RequestContext.RequestMessage;
            var prop = (HttpRequestMessageProperty)request.Properties[HttpRequestMessageProperty.Name];
            var acceptHeader = prop.Headers[HttpRequestHeader.Accept] ?? prop.Headers[HttpRequestHeader.ContentType];

            // Parse accept header
            var acceptedMimes = AcceptHeaderParser.Parse(acceptHeader);

            // Because we want to match patterns, look-up by mime type is not a way to go.
            // Loop over each item instead.

            IDispatchMessageFormatter formatter = null;
            
            for (int i = 0; i < acceptedMimes.Length; i++)
            {
                foreach (var formatMime in formatters.Keys)
                {
                    if (acceptedMimes[i].IsMatching(formatMime))
                    {
                        formatter = formatters[formatMime];
                        break;
                    }
                }
            }

            if (formatter == null)
            {
                formatter = fallbackFormatter;
            }

            return formatter.SerializeReply(messageVersion, parameters, result);
        }
    }
}