﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace Jhu.Graywulf.Web.Services
{
    class StreamingListFormatter : GraywulfMessageFormatter, IDispatchMessageFormatter, IClientMessageFormatter
    {

        private IDispatchMessageFormatter fallbackDispatchMessageFormatter;
        private IClientMessageFormatter fallbackClientMessageFormatter;
        private Type returnType;

        public StreamingListFormatter(IDispatchMessageFormatter dispatchMessageFormatter)
        {
            InitializeMembers();

            this.fallbackDispatchMessageFormatter = dispatchMessageFormatter;
        }

        public StreamingListFormatter(IClientMessageFormatter clientMessageFormatter, Type returnType)
        {
            InitializeMembers();

            this.fallbackClientMessageFormatter = clientMessageFormatter;
            this.returnType = returnType;
        }

        private void InitializeMembers()
        {
            this.fallbackClientMessageFormatter = null;
            this.fallbackClientMessageFormatter = null;
            this.returnType = null;
        }

        public override string[] GetSupportedMimeTypes()
        {
            return new string[] { 
                Constants.MimeTypeXml,
                Constants.MimeTypeJson,
            };
        }

        public override void DeserializeRequest(Message message, object[] parameters)
        {
            fallbackDispatchMessageFormatter.DeserializeRequest(message, parameters);
        }

        public override Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
        {
            var writer = new StreamingListXmlMessageBodyWriter(result);
            var message = WebOperationContext.Current.CreateStreamResponse(writer, Constants.MimeTypeXml);
            return message;
        }

        public override Message SerializeRequest(MessageVersion messageVersion, object[] parameters)
        {
            return fallbackClientMessageFormatter.SerializeRequest(messageVersion, parameters);
        }

        public override object DeserializeReply(Message message, object[] parameters)
        {
            var reader = new StreamingListXmlMessageBodyReader(returnType);
            return reader.ReadBodyContents(message.GetReaderAtBodyContents());
        }

        internal static void ReflectClass(Type type, out string name, out string ns, out Dictionary<string, PropertyInfo> properties)
        {
            // No attribute present
            // <ArrayOffootprint xmlns:i="http://www.w3.org/2001/XMLSchema-instance">

            // Inside class:
            // <footprintList xmlns="" xmlns:i="http://www.w3.org/2001/XMLSchema-instance"><footprints>
            // <footprint>....

            name = ns = null;

            // Look for any attributes that control serialization of name
            var attrs = type.GetCustomAttributes(true);

            for (int i = 0; i < attrs.Length; i++)
            {
                var attr = attrs[i];

                if (attr is XmlRootAttribute)
                {
                    var a = (XmlRootAttribute)attr;
                    name = a.ElementName;
                    ns = a.Namespace;
                }
                else if (attr is DataContractAttribute)
                {
                    var a = (DataContractAttribute)attr;
                    name = a.Name;
                    ns = a.Namespace;
                }
            }

            name = name ?? type.Name;
            ns = ns ?? "http://schemas.datacontract.org/2004/07/" + type.Namespace;

            properties = ReflectProperties(type);
        }

        internal static Dictionary<string, PropertyInfo> ReflectProperties(Type type)
        {
            // Iterate through properties
            var res = new Dictionary<string, PropertyInfo>(StringComparer.InvariantCultureIgnoreCase);
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.SetProperty);

            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];

                string propName;
                StreamingListFormatter.ReflectProperty(prop, out propName);

                res.Add(propName, prop);
            }

            return res;
        }

        internal static void ReflectProperty(PropertyInfo prop, out string name)
        {
            name = null;

            // Look for any attributes that control serialization of name
            var attrs = prop.GetCustomAttributes(true);

            for (int i = 0; i < attrs.Length; i++)
            {
                var attr = attrs[i];

                if (attr is XmlElementAttribute)
                {
                    var a = (XmlElementAttribute)attr;
                    name = a.ElementName;
                }
                else if (attr is DataMemberAttribute)
                {
                    var a = (DataMemberAttribute)attr;
                    name = a.Name;
                }
            }
        }
    }
}
