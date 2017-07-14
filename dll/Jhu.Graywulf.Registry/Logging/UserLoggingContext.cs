﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Jhu.Graywulf.Logging
{
    public abstract class UserLoggingContext : LoggingContext
    {

        public UserLoggingContext(LoggingContext outerContext)
                : base(outerContext)
        {
            InitializeMembers(new StreamingContext());
        }

        public UserLoggingContext(UserLoggingContext outerContext)
                : base(outerContext)
        {
            CopyMembers(outerContext);
        }

        [OnDeserializing]
        private void InitializeMembers(StreamingContext context)
        {
        }

        private void CopyMembers(UserLoggingContext outerContext)
        {
        }

        public override void UpdateEvent(Event e)
        {
            base.UpdateEvent(e);

            Guid userGuid = Guid.Empty;
            Guid sessionGuid = Guid.Empty;

            var principal = e.Principal as Jhu.Graywulf.AccessControl.GraywulfPrincipal;

            if (principal != null)
            {
                userGuid = principal.Identity.UserReference.Guid;
                Guid.TryParse(principal.Identity.SessionId, out sessionGuid);
            }

            if (sessionGuid != Guid.Empty) e.SessionGuid = sessionGuid;
            if (userGuid != Guid.Empty) e.UserGuid = userGuid;
        }
    }
}