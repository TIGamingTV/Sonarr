using System;
using System.Net;
using NzbDrone.Core.Exceptions;

namespace NzbDrone.Core.MetadataSource.Tmdb
{
    public class TmdbException : NzbDroneClientException
    {
        public TmdbException(string message)
            : base(HttpStatusCode.ServiceUnavailable, message)
        {
        }

        public TmdbException(string message, params object[] args)
            : base(HttpStatusCode.ServiceUnavailable, message, args)
        {
        }

        public TmdbException(string message, Exception innerException, params object[] args)
            : base(HttpStatusCode.ServiceUnavailable, message, innerException, args)
        {
        }
    }
}
