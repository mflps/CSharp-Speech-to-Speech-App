using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.MT.Api.TestUtils
{
    public  class HttpsCertificateValidator
    {
        /// <summary>
        /// Validates microsofttranslator certificates used for authentication
        /// </summary>
        public static bool ValidateServerCertificate(Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // Example: CN=api.microsofttranslator.com, OU=Bing, O=Microsoft, L=Redmond, C=US
            int i1 = certificate.Subject.IndexOf("CN=");
            if (i1 > -1)
            {
                int i2 = certificate.Subject.IndexOf(",", i1);
                if (i2 == -1)
                {
                    i2 = certificate.Subject.Length;
                }
                string cn = certificate.Subject.Substring(i1, i2 - i1);
                if (cn.Contains(".microsofttranslator-int.com") ||
                    cn.Contains(".microsofttranslator.com"))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
