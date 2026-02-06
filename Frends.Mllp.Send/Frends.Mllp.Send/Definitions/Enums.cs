using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Frends.Mllp.Send.Definitions
{
    /// <summary>
    /// Defines the encryption and authentication level for the MLLP connection.
    /// </summary>
    public enum TlsMode
    {
        /// <summary>
        /// No encryption. Data is sent in plain text (standard TCP).
        /// </summary>
        None,

        /// <summary>
        /// Mutual TLS. Both client and server must provide valid certificates to establish a secure, encrypted connection.
        /// </summary>
        Mtls,
    }
}
