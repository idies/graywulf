﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.IO;

namespace Jhu.Graywulf.Web.Services
{
    public abstract class StreamingRawFormatter<T> : StreamingRawFormatterBase, IDispatchMessageFormatter, IClientMessageFormatter
    {
        internal override Type FormattedType
        {
            get
            {
                return typeof(T);
            }
        }


        protected StreamingRawFormatter(IDispatchMessageFormatter dispatchMessageFormatter)
            : base(dispatchMessageFormatter)
        {
        }

        protected StreamingRawFormatter(IClientMessageFormatter clientMessageFormatter)
            : base(clientMessageFormatter)
        {
        }

        private WebHeaderCollection GetRequestHeaders()
        {
            var message = OperationContext.Current.RequestContext.RequestMessage;
            return GetRequestHeaders(message);
        }

        private WebHeaderCollection GetRequestHeaders(Message message)
        {
            var prop = (HttpRequestMessageProperty)message.Properties[HttpRequestMessageProperty.Name];
            return prop.Headers;
        }

        private WebHeaderCollection GetResponseHeaders(Message message)
        {
            var prop = (HttpResponseMessageProperty)message.Properties[HttpResponseMessageProperty.Name];
            return prop.Headers;
        }

        protected abstract StreamingRawAdapter<T> CreateAdapter();

        public override string[] GetSupportedMimeTypes()
        {
            return CreateAdapter().GetSupportedMimeTypes();
        }

        #region Server

        public override void DeserializeRequest(Message message, object[] parameters)
        {
            if (!message.IsEmpty &&
                (Direction & StreamingRawFormatterDirection.Parameters) != 0)
            {
                var body = message.GetReaderAtBodyContents();
                byte[] raw = body.ReadContentAsBase64();

                using (var ms = new MemoryStream(raw))
                {
                    var adapter = CreateAdapter();
                    adapter.Headers = GetRequestHeaders(message);
                    parameters[parameters.Length - 1] = adapter.ReadFromStream(ms);
                }
            }
            else
            {
                base.DeserializeRequest(message, parameters);
            }
        }

        public override Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
        {
            if ((Direction & StreamingRawFormatterDirection.ReturnValue) != 0)
            {
                var adapter = CreateAdapter();
                adapter.Headers = GetRequestHeaders();
                var contentType = adapter.GetRequestedContentType();

                var message = WebOperationContext.Current.CreateStreamResponse(
                    stream =>
                    {
                        adapter.WriteToStream(stream, (T)result);
                    },
                    contentType);

                return message;
            }
            else
            {
                return base.SerializeReply(messageVersion, parameters, result);
            }
        }

        #endregion
        #region Client

        public override Message SerializeRequest(MessageVersion messageVersion, object[] parameters)
        {
            /*
            if ((Direction & StreamingRawFormatterDirection.Parameters) != 0)
            {
                var adapter = CreateAdapter();
                var contentType = adapter.GetRequestedContentType();

                
            }*/

            return base.SerializeRequest(messageVersion, parameters);
        }

        public override object DeserializeReply(Message message, object[] parameters)
        {
            if ((Direction & StreamingRawFormatterDirection.ReturnValue) != 0)
            {
                var body = message.GetReaderAtBodyContents();
                byte[] raw = body.ReadContentAsBase64();

                using (var ms = new MemoryStream(raw))
                {
                    var adapter = CreateAdapter();
                    adapter.Headers = GetResponseHeaders(message);
                    return adapter.ReadFromStream(ms);
                }
            }
            else
            {
                return base.DeserializeReply(message, parameters);
            }
        }

        #endregion
    }
}
