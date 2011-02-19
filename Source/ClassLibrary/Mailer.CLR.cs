/*

 Copyright (c) 2004-2010 Pavel Novak, Tomas Matousek and Daniel Balas.

 The use and distribution terms for this software are contained in the file named License.txt, 
 which can be found in the root of the Phalanger distribution. By using this software 
 in any fashion, you are agreeing to be bound by the terms of this license.
 
 You must not remove this notice from this software.

 TODO: .NET 2.0

*/
using System;
using PHP.Core;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

namespace PHP.Library
{
	/// <summary>
	/// Maps PHP mailing methods to Framework ones.
	/// </summary>
	/// <threadsafety static="true"/>
	public static class Mailer
	{
		#region mail, ezmlm_hash

		/// <summary>
		/// Sends e-mail only with essential headers.
		/// </summary>
		/// <param name="to">Recipient e-mail address.</param>
		/// <param name="subject">E-mail subject.</param>
		/// <param name="message">Message body.</param>
		/// <returns>True if mail was accepted to send.</returns>
		[ImplementsFunction("mail")]
        public static bool Mail(string to, string subject, string message)
		{
			return Mail(to, subject, message, null);
		}

		/// <summary>
		/// Sends e-mail, allows specify additional headers.
		/// Supported are Cc, Bcc, From, Priority, Content-type. Others are ignored.
		/// </summary>
		/// <remarks>
		/// E-mail addresses specified in additional headers must be valid (including From header).
		/// Otherwise your e-mail will not be send.
		/// </remarks>
		/// <param name="to">Recipient e-mail address.</param>
		/// <param name="subject">E-mail subject.</param>
		/// <param name="message">Message body.</param>
		/// <param name="additionalHeaders">Additional headers.</param>
		/// <returns>True if mail was accepted to send.</returns>
		[ImplementsFunction("mail")]
        public static bool Mail(string to, string subject, string message, string additionalHeaders)
		{
			// to and subject cannot contain newlines, replace with spaces
			to = (to != null) ? to.Replace("\r\n", " ").Replace('\n', ' ') : "";
			subject = (subject != null) ? subject.Replace("\r\n", " ").Replace('\n', ' ') : "";

			Debug.WriteLine("MAILER", "mail('{0}','{1}','{2}','{3}')", to, subject, message, additionalHeaders);

			// get current configuration, we need some fields for mailing
			LibraryConfiguration config = LibraryConfiguration.Local;

			// set basic mail fields
            string from = null;

			if (!String.IsNullOrEmpty(config.Mailer.DefaultFromHeader))
			{
				try
				{
					from = config.Mailer.DefaultFromHeader;
				}
				catch (FormatException ex)
				{
                    PhpException.Throw(PhpError.Warning, LibResources.GetString("cannot_send_email", ex.Message));
					return false;
				}
			}

			// set SMTP server we are using
            RawSmtpClient client = new RawSmtpClient(config.Mailer.SmtpServer, config.Mailer.SmtpPort);

            try
            {
                client.Connect();
                client.SendMessage(from, new string[] { to }, subject, new string[] { "X-PHP-Originating-Script: 1:" + ScriptContext.CurrentContext.MainScriptFile.RelativePath.Path, additionalHeaders }, message);
                return true;
            }
            catch (Exception e)
            {
                string error_message = e.Message;
                Exception inner = e;
                while ((inner = inner.InnerException) != null)
                    error_message += "; " + inner.Message;

                PhpException.Throw(PhpError.Warning, LibResources.GetString("cannot_send_email", error_message));
                return false;
            }
            finally
            { 
                client.Disconnect(); 
            }
		}


		/// <summary>
		/// Sends e-mail, allows specify additional headers and additional parameters.
		/// </summary>
		/// <remarks>
		/// Additional parameters are not supported, must be null or empty string.
		/// Use overload function without <c>additionalParameters</c> parameter.
		/// </remarks>
		/// <param name="to">Recipient e-mail address.</param>
		/// <param name="subject">E-mail subject.</param>
		/// <param name="message">Message body.</param>
		/// <param name="additionalHeaders">Additional headers.</param>
		/// <param name="additionalParameters">Additional parameters.</param>
		/// <returns>True if mail was accepted to send.</returns>
		[ImplementsFunction("mail")]
		public static bool Mail(string to, string subject, string message, string additionalHeaders, string additionalParameters)
		{
			// additional parameters are not supported while running windows
			if (!string.IsNullOrEmpty(additionalParameters))
				PhpException.Throw(PhpError.Warning, LibResources.GetString("additional_parameters_not_supported"));

			return Mail(to, subject, message, additionalHeaders);
		}

		/// <summary>
		/// Counts hash value needed by EZMLM.
		/// </summary>
		/// <param name="addr">Mail address for which is hash value calculating.</param>
		/// <returns>Calculated hash value.</returns>
		[ImplementsFunction("ezmlm_hash")]
		public static int ezmlm_hash(string addr)
		{
			// this algorithm is assumed from PHP source code

			uint h = 5381; // must be 32-bit unsigned
			addr = addr.ToLower();

			unchecked // overflow may occur, this is OK.
			{
				for (int j = 0; j < addr.Length; j++)
				{
					h = (h + (h << 5)) ^ (uint)addr[j];
				}
			}

			h = (h % 53);

			return (int)h;
		}

		#endregion

		#region Mail headers parsing

		/// <summary>
		/// Extracts mail headers from string <c>headers</c> and if the string contains supported headers,
		/// appropriate fields are set to <c>MailMessage mm</c> object.
		/// Supported headers are: Cc, Bcc, From, Priority, Content-type. Others are ignored.
		/// </summary>
		/// <param name="headers">String containing mail headers.</param>
		/// <param name="mm">MailMessage object to set fields according to <c>headers</c>.</param>
		private static void SetMailHeaders(string headers, MailMessage mm)
		{
			// parse additional headers
			Regex headerRegex = new Regex("^([^:]+):[ \t]*(.+)$");
			Match headerMatch;

			int line_begin, line_end = -1;
			while (true)
			{
				line_begin = line_end + 1;

				// search for non-empty line
				while (line_begin < headers.Length && (headers[line_begin] == '\n' || headers[line_begin] == '\r'))
					line_begin++;
				if (line_begin >= headers.Length)
					break;

				// find the line end
				line_end = line_begin + 1;
				while (line_end < headers.Length && headers[line_end] != '\n' && headers[line_end] != '\r')
					line_end++;

				string header = headers.Substring(line_begin, line_end - line_begin);
				headerMatch = headerRegex.Match(header);

				// ignore wrong formatted headers
				if (!headerMatch.Success)
					continue;

				string sw = headerMatch.Groups[1].Value.Trim().ToLower();
				switch (sw)
				{
					case "cc":
						mm.CC.Add(ExtractMailAddressesOnly(headerMatch.Groups[2].Value, Int32.MaxValue));
						break;
					case "bcc":
						mm.Bcc.Add(ExtractMailAddressesOnly(headerMatch.Groups[2].Value, Int32.MaxValue));
						break;
					case "from":
						string from = ExtractMailAddressesOnly(headerMatch.Groups[2].Value, 1);
						if (!String.IsNullOrEmpty(from))
						{
							try
							{
								mm.From = new MailAddress(from);
							}
							catch (FormatException)
							{ }
						}
						break;
					case "priority":
						mm.Priority = ExtractPriority(headerMatch.Groups[2].Value);
						break;
					case "content-type":
						ExtractContentType(headerMatch.Groups[2].Value, mm);
						break;

					default:
						mm.Headers.Add(headerMatch.Groups[1].Value.Trim(), headerMatch.Groups[2].Value);
						break;
				}
			}
		}

		/// <summary>
		/// Converts semicolon separated list of email addresses and names of email owners
		/// to semicolon separated list of only email addresses.
		/// </summary>
		/// <param name="emails">Semicolon separated list of email addresses and names.</param>
		/// <param name="max">Max number of emails returned.</param>
		/// <returns>Semicolon separated list of email addresses only.</returns>
		private static string ExtractMailAddressesOnly(string emails, int max)
		{
			StringBuilder mailsOnly = new StringBuilder();
			Regex regWithName = new Regex("^[ \t]*([^<>]*?)[ \t]*<[ \t]*([^<>]*?)[ \t]*>[ \t]*$");
			Regex regEmail = new Regex("^[ \t]*[^@ \t<>]+@[^@ \t<>]+.[^@ \t<>]+[ \t]*$");

			Match m, m2;
			string toAppend = "";
			string[] mailsArray = emails.Split(';');
			foreach (string mail in mailsArray)
			{
				m = regWithName.Match(mail);
				if (m.Success) // mail with name
				{
					Group gr;
					for (int i = 1; i < m.Groups.Count; i++)
					{
						gr = m.Groups[i];
						m2 = regEmail.Match(gr.Value);
						if (m2.Success)
						{
							toAppend = m2.Value;
						}
					}
					// if an e-mail is in <..> we forget previous email found out of <..> (the name looks like e-mail address)
					mailsOnly.Append(toAppend);
					mailsOnly.Append(';');
				}
				else
				{
					m2 = regEmail.Match(mail);
					if (m2.Success) // only email without name
					{
						mailsOnly.Append(m2.Value);
						mailsOnly.Append(';');
					}
					else
					{
						// bad e-mail address
						PhpException.Throw(PhpError.Warning, LibResources.GetString("invalid_email_address", mail));
					}
				}
			}

			if (mailsOnly.Length == 0)
				return "";

			// return without last semicolon
			return mailsOnly.ToString(0, mailsOnly.Length - 1);
		}

		/// <summary>
		/// Used for converting header Priority to <c>MailPriority</c> value needed by .NET Framework mailer.
		/// </summary>
		/// <param name="p">"Priority:" header value.</param>
		/// <returns><c>MailPriority</c> specified by header value.</returns>
		private static MailPriority ExtractPriority(string p)
		{
			switch (p.Trim().ToLower())
			{
				case "high":
					return MailPriority.High;
				case "low":
					return MailPriority.Low;
				case "normal":
					return MailPriority.Normal;
				default:
					goto case "normal";
			}
		}

		/// <summary>
		/// Used for converting header ContentType to <c>MailFormat</c> value and <c>Encoding</c> class.
		/// </summary>
		/// <param name="contentTypeHeader">"Content-type:" header value</param>
        /// <param name="mm">Mail message instance.</param>
		private static void ExtractContentType(string contentTypeHeader, MailMessage mm)
		{
            contentTypeHeader = contentTypeHeader.Trim().ToLower();

            // extract content-type value parts (type/subtype; parameter1=value1; parameter2=value2)
            string[] headerParts = contentTypeHeader.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (headerParts == null || headerParts.Length == 0)
                return;

            // process type/subtype
            mm.IsBodyHtml = (headerParts[0].Trim() == "text/html");
            
            for (int i = 1; i < headerParts.Length; ++i)
            {
                int asspos = headerParts[i].IndexOf('=');
                if (asspos < 1) continue;

                string propertyName = headerParts[i].Remove(asspos).Trim();
                string propertyValue = headerParts[i].Substring(asspos + 1).Trim(new char[]{' ','\t','\"','\'','\n','\r'});

                switch (propertyName)
                {
                    case "charset":
                        try
                        {
                            mm.BodyEncoding = Encoding.GetEncoding(propertyValue);
                        }
                        catch (Exception)
                        {}
                        break;
                    default:
                        break;
                }
            }
            
            // add header into the mail message as it is
            mm.Headers.Add("content-type", contentTypeHeader);
		}
		#endregion

        #region RawSmtpClient
        /*
         
         Copyright (c) 2010 Daniel Balas

         The use and distribution terms for this software are contained in the file named License.txt, 
         which can be found in the root of the Phalanger distribution. By using this software 
         in any fashion, you are agreeing to be bound by the terms of this license.
         
         You must not remove this notice from this software.

        */

        /// <summary>
        /// Raw SMTP client serving the needs of PHP mail functions. This is reimplemented mainly because .NET SmtpClient provides
        /// certain level of abstraction which is incompatible with mail function usage. Currently not as much advanced, but it can easily be.
        /// </summary>
        internal class RawSmtpClient
        {
            /// <summary>
            /// Wait time for Socket.Poll - in microseconds.
            /// </summary>
            private const int _pollTime = 100000;

            /// <summary>
            /// Timeout of connection. We don't want to block for too long.
            /// </summary>
            private const int _connectionTimeout = 5000;

            /// <summary>
            /// Gets a value indicating whether this client is connected to a server.
            /// </summary>
            public bool Connected { get { return _connected; } }
            private bool _connected;

            /// <summary>
            /// Gets or sets a value indicating whether this client should implicitly use ESMTP to connect to the server.
            /// </summary>
            public bool UseExtendedSmtp { get { return _useExtendedSmtp; } set { _useExtendedSmtp = value; } }
            private bool _useExtendedSmtp;

            /// <summary>
            /// Gets host name set for this client to connect to.
            /// </summary>
            public string HostName { get { return _hostName; } }
            private string _hostName;

            /// <summary>
            /// Gets port number set for this client to connect to.
            /// </summary>
            public int Port { get { return _port; } }
            private int _port;

            /// <summary>
            /// Gets a list of SMTP extensions supported by current connection.
            /// </summary>
            public string[] Extensions { get { return _extensions; } }
            private string[] _extensions;

            private TextReader _reader;
            private TextWriter _writer;

            private Socket _socket;
            private NetworkStream _stream;

            public RawSmtpClient(string hostName)
                : this(hostName, 25)
            {
            }

            /// <summary>
            /// Initializes a new instance of AdvancedSmtp client class.
            /// </summary>
            /// <param name="hostName">Host name (IP or domain name) of the SMTP server.</param>
            /// <param name="port">Port on which SMTP server runs.</param>
            public RawSmtpClient(string hostName, int port)
            {
                _hostName = hostName;
                _port = port;
                _connected = false;
                _useExtendedSmtp = true;
            }

            /// <summary>
            /// Resets the state of this object.
            /// </summary>
            private void ResetConnection()
            {
                if (_reader != null)
                {
                    _reader.Close();
                    _reader = null;
                }

                if (_writer != null)
                {
                    _writer.Close();
                    _writer = null;
                }

                if (_stream != null)
                {
                    _stream.Close();
                    _stream = null;
                }

                if (_socket != null)
                {
                    if (_socket.Connected)
                        _socket.Shutdown(SocketShutdown.Both);
                    _socket.Close();
                    _socket = null;
                }

                _extensions = null;
                _connected = false;
            }

            /// <summary>
            /// Connects to the server.
            /// </summary>
            /// <remarks>Method throws an exception on any error.</remarks>
            /// <exception cref="SmtpException">If any error occures.</exception>
            public void Connect()
            {
                // invariant condition
                Debug.Assert(_connected == (_socket != null));

                // check whether socket is not already connected
                if (_connected)
                {
                    // check whether the socket is OK
                    bool error = _socket.Poll(_pollTime, SelectMode.SelectError);

                    if (!error)
                        // ok, we keep this connection
                        return;// true;

                    // close the socket and reset
                    ResetConnection();
                }

                // resolve host's domain
                IPAddress[] addresses = null;

                try
                {
                    addresses = System.Net.Dns.GetHostAddresses(_hostName);
                }
                catch (Exception e)
                {
                    // DNS error - reset and fail
                    ResetConnection();
                    //return false;
                    throw new SmtpException(e.Message);
                }

                Debug.Assert(addresses != null);

                // create socket
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // begin async connect
                IAsyncResult res = _socket.BeginConnect(addresses, _port, null, null);

                // wait for some time
                res.AsyncWaitHandle.WaitOne(_connectionTimeout);

                // if socket could not connect, reset and fail
                if (!_socket.Connected)
                {
                    ResetConnection();
                    throw new SmtpException("Cannot connect to " + _hostName);
                }

                // if anything inside throws exception, we were not successful
                try
                {
                    // end connect
                    _socket.EndConnect(res);

                    // create a stream
                    _stream = new NetworkStream(_socket);

                    // create _reader and _writer
                    _reader = new StreamReader(_stream, Encoding.ASCII);
                    _writer = new StreamWriter(_stream, Encoding.ASCII);
                    _writer.NewLine = "\r\n";

                    string line;

                    // read server welcome message
                    line = _reader.ReadLine();

                    // if there is no 220 in the beginning, this is no SMTP server
                    if (!line.StartsWith("220")) throw new SmtpException("Expected 220, '"+line+"' given");// return false;
                    //TODO: server name processing

                    // send ESMTP welcome message
                    if (_useExtendedSmtp)
                    {
                        _writer.WriteLine("EHLO " + System.Net.Dns.GetHostName());

                        // flush the stream
                        _writer.Flush();

                        // read response
                        line = _reader.ReadLine();
                    }

                    if (_useExtendedSmtp && line.StartsWith("250"))
                    {
                        // this is ESMTP server

                        // ESMTP returns '-' on fourth char if there are any more lines available
                        if (line[3] == ' ')
                        {
                            // there are no extensions
                            _extensions = ArrayUtils.EmptyStrings;

                            // success
                            return;// true;
                        }
                        else if (line[3] == '-')
                        {
                            List<string> extensions = new List<string>();

                            // we do not need to read first line - there is only a welcome string

                            while (true)
                            {
                                //read new line
                                line = _reader.ReadLine();

                                if (line.StartsWith("250-"))
                                {
                                    //add new extension name
                                    extensions.Add(line.Substring(4, line.Length - 4));
                                }
                                else if (line.StartsWith("250 "))
                                {
                                    //add new extension name and finish handshake
                                    extensions.Add(line.Substring(4, line.Length - 4));
                                    _extensions = extensions.ToArray();
                                    _connected = true;
                                    return;// true;
                                }
                                else
                                {
                                    //invalid response (do not send QUIT message)
                                    break;
                                }
                            }
                        }

                        // this is not a valid ESMTP server
                    }
                    else if (line.StartsWith("500") || !_useExtendedSmtp)
                    {
                        // no need to send another HELO if we have already sent one
                        _writer.WriteLine("HELO " + System.Net.Dns.GetHostName());

                        line = _reader.ReadLine();

                        if (line.StartsWith("250"))
                        {
                            _extensions = ArrayUtils.EmptyStrings;

                            // handshake complete
                            _connected = true;
                            return;// true;
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new SmtpException(e.Message);
                } // any error is bad

                ResetConnection(); // (do not send QUIT message)

                throw new SmtpException("Unexpected"); //return false;
            }

            /// <summary>
            /// Disconnects the client from the server.
            /// </summary>
            public void Disconnect()
            {
                if (!_connected)
                {
                    ResetConnection();
                    return;
                }

                _writer.WriteLine("QUIT");

                // flush the stream
                _writer.Flush();

                string line = _reader.ReadLine();

                if (!line.StartsWith("221"))
                {
                    //incorrect response (do nothing)
                }

                //correct response
                ResetConnection();
            }

            /// <summary>
            /// Sends reset message to the server.
            /// </summary>
            private void Reset()
            {
                if (!_connected) return;

                if (_reader.Peek() != -1)
                {
                    // there is something on the input (should be empty)
                    ResetConnection();
                    return;
                }

                _writer.WriteLine("RSET");

                // flush the stream
                _writer.Flush();

                string line = _reader.ReadLine();

                if (!line.StartsWith("250"))
                {
                    //invalid response
                    ResetConnection();
                }
            }

            /// <summary>
            /// Prepares the data lines from supplied message properties. All data will be send as
            /// ASCII if possible, otherwise 
            /// </summary>
            /// <param name="from">Sender of the mail.</param>
            /// <param name="to">Recipients of the mail.</param>
            /// <param name="subject">Subject of the mail.</param>
            /// <param name="headers">Additional headers.</param>
            /// <param name="body">Message body.</param>
            /// <returns></returns>
            private string[] PrepareMessageData(string from, string[] to, string subject, string[] headers, string body)
            {
                Dictionary<string, int> headerHashtable = new Dictionary<string, int>();
                List<KeyValuePair<string, string>> headerList = new List<KeyValuePair<string, string>>();

                //parse headers
                foreach (string header in headers)
                {
                    StringReader reader = new StringReader(header);

                    while (reader.Peek() != -1)
                    {
                        string line = reader.ReadLine();
                        int index = line.IndexOf(": ");

                        if (index == -1)
                        {
                            continue;
                        }
                        else
                        {
                            string name = line.Substring(0, index);
                            string value = line.Substring(index + 2);

                            if (headerHashtable.ContainsKey(name))
                            {
                                headerHashtable[name] = headerList.Count;
                            }
                            else
                            {
                                headerHashtable.Add(name, headerList.Count);
                            }

                            headerList.Add(new KeyValuePair<string, string>(name, value));
                        }
                    }
                }

                List<string> ret = new List<string>();

                ret.Add("Date: " + DateTime.Now.ToString("ddd, dd MMM yyyy HH:mm:ss zz00", new CultureInfo("en-US")));
                ret.Add("Subject: " + subject);

                StringBuilder recipients = new StringBuilder();

                for (int i = 0; i < to.Length; i++)
                {
                    if (i != 0) recipients.Append(", ");

                    recipients.Append(to[i]);
                }

                ret.Add("To: " + recipients);

                for (int i = 0; i < headerList.Count; i++)
                {
                    var header = headerList[i];

                    if (headerHashtable[header.Key] == i)
                    {
                        ret.Add(header.Key + ": " + header.Value);
                    }
                }

                ret.Add("");

                StringReader bodyReader = new StringReader(body);

                while (bodyReader.Peek() != -1)
                    ret.Add(bodyReader.ReadLine());

                return ret.ToArray();
            }

            /// <summary>
            /// Sends the raw message.
            /// </summary>
            /// <remarks>On eny error an exception is thrown.</remarks>
            /// <exception cref="SmtpException">When any error occures during the mail send.</exception>
            public void SendMessage(string from, string[] to, string subject, string[] headers, string body)
            {
                //
                // see http://email.about.com/cs/standards/a/smtp_error_code_2.htm for response codes.
                //

                if (!_connected)
                    throw new SmtpException("NOT CONNECTED");
                
                // start mail transaction
                _writer.WriteLine("MAIL FROM:" + from);

                // flush the stream
                _writer.Flush();

                string line = _reader.ReadLine();

                if (!line.StartsWith("250"))
                {
                    Reset();
                    throw new SmtpException(string.Format("Expected response {0}, '{1}' given.", 250, line));
                }

                foreach (string recipientstr in to)
                {
                    var recipient = new MailAddress(recipientstr);
                    _writer.WriteLine("RCPT TO:" + recipient.Address);

                    // flush the stream
                    _writer.Flush();

                    line = _reader.ReadLine();

                    if (!(line.StartsWith("250") || line.StartsWith("251")))
                    {
                        Reset();
                        throw new SmtpException(string.Format("Expected response {0}, '{1}' given.", "250 or 251", line));
                    }
                }

                _writer.WriteLine("DATA");

                // flush the stream
                _writer.Flush();

                line = _reader.ReadLine();

                if (!line.StartsWith("354"))
                {
                    Reset();
                    throw new SmtpException(string.Format("Expected response {0}, '{1}' given.", 354, line));
                }

                //prepare data that is broken up to form data lines.
                string[] dataLines = PrepareMessageData(from, to, subject, headers ?? ArrayUtils.EmptyStrings, body);

                foreach (string dataLine in dataLines)
                {
                    // PHP implementation uses 991 line length limit (including CRLF)
                    int maxLineLength = 989;
                    int lineStart = 0;
                    int correction = 0;

                    // if SP character is on the first place, we need to duplicate it
                    if (dataLine.Length > 0 && dataLine[0] == '.')
                    {
                        _writer.Write('.');
                    }

                    // according to MIME, the lines must not be longer than 998 characters (1000 including CRLF)
                    // so we need to break such lines using folding
                    while (dataLine.Length - lineStart > maxLineLength - correction)
                    {
                        //break the line, inserting FWS sequence
                        _writer.WriteLine(dataLine.Substring(lineStart, maxLineLength - correction));
                        _writer.Write(' ');
                        lineStart += maxLineLength - correction;

                        //make correction (whitespace on the next line)
                        correction += 1;
                    }

                    //output the rest of the line
                    _writer.WriteLine(dataLine.Substring(lineStart));

                    // flush the stream
                    _writer.Flush();
                }

                _writer.WriteLine(".");

                // flush the stream
                _writer.Flush();

                line = _reader.ReadLine();

                if (!line.StartsWith("250"))
                {
                    Reset();
                    throw new SmtpException(string.Format("Expected response {0}, '{1}' given.", 250, line));
                }

                //return true; // ok
            }
        }

        #endregion
	}
}
