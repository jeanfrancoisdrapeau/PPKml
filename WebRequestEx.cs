using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;

namespace PPKml
{
	public class WebRequestEx
	{
		/// <summary>
		/// Base class for state object that gets passed around amongst async methods 
		/// when doing async web request/response for data transfer.  We store basic 
		/// things that track current state of a download, including # bytes transfered,
		/// as well as some async callbacks that will get invoked at various points.
		/// </summary>
		abstract public class WebRequestState
		{
			public int bytesRead;           // # bytes read during current transfer
			public long totalBytes;		    // Total bytes to read
			public double progIncrement;	// delta % for each buffer read
			public Stream streamResponse;	// Stream to read from 
			public byte[] bufferRead;	    // Buffer to read data into
			public byte[] bufferData;       // Data buffer that holds all the data
			public Uri fileURI;		        // Uri of object being downloaded
			public string FTPMethod;	    // What was the previous FTP command?  (e.g. get file size vs. download)
			public DateTime transferStart;  // Used for tracking xfr rate

			// Callbacks for response packet info & progress
			public ResponseInfoDelegate respInfoCB;
			//public ProgressDelegate progCB;
			public DoneDelegate doneCB;

			private WebRequest _request;
			public virtual WebRequest request
			{
				get { return null; }
				set { _request = value; }
			}

			private WebResponse _response;
			public virtual WebResponse response
			{
				get { return null; }
				set { _response = value; }
			}

			public WebRequestState(int buffSize)
			{
				bytesRead = 0;
				bufferRead = new byte[buffSize];
				bufferData = null;
				streamResponse = null;
			}
		}

		/// <summary>
		/// State object for HTTP transfers
		/// </summary>
		public class HttpWebRequestState : WebRequestState
		{
			private HttpWebRequest _request;
			public override WebRequest request
			{
				get
				{
					return _request;
				}
				set
				{
					_request = (HttpWebRequest)value;
				}
			}

			private HttpWebResponse _response;
			public override WebResponse response
			{
				get
				{
					return _response;
				}
				set
				{
					_response = (HttpWebResponse)value;
				}
			}

			public HttpWebRequestState(int buffSize) : base(buffSize) { }
		}

		public delegate void ResponseInfoDelegate(string statusDescr, string contentLength);
		public delegate void ProgressDelegate(int totalBytes, double pctComplete, double transferRate);
		public delegate void DoneDelegate();

		public const int BUFFER_SIZE = 1448;

		/// <summary>
		/// Main response callback, invoked once we have first Response packet from
		/// server.  This is where we initiate the actual file transfer, reading from
		/// a stream.
		/// </summary>
		public static void RespCallback(IAsyncResult asyncResult)
		{
			try
			{
				WebRequestState reqState = ((WebRequestState)(asyncResult.AsyncState));
				WebRequest req = reqState.request;
				string statusDescr = "";
				//string contentLength = "";

				// HTTP 
				HttpWebResponse resp = ((HttpWebResponse)(req.EndGetResponse(asyncResult)));
				reqState.response = resp;
				statusDescr = resp.StatusDescription;
				reqState.totalBytes = reqState.response.ContentLength;
				//contentLength = reqState.response.ContentLength.ToString();   // # bytes

				// Set up a stream, for reading response data into it
				Stream responseStream = reqState.response.GetResponseStream();
				reqState.streamResponse = responseStream;

				// Begin reading contents of the response data
				responseStream.BeginRead(reqState.bufferRead, 0, BUFFER_SIZE, new AsyncCallback(ReadCallback), reqState);

				return;
			}
			catch
			{
			}
		}

		/// <summary>
		/// Main callback invoked in response to the Stream.BeginRead method, when we have some data.
		/// </summary>
		private static void ReadCallback(IAsyncResult asyncResult)
		{
			try
			{
				// Will be either HttpWebRequestState or FtpWebRequestState
				WebRequestState reqState = ((WebRequestState)(asyncResult.AsyncState));

				Stream responseStream = reqState.streamResponse;

				// Get results of read operation
				int bytesRead = responseStream.EndRead(asyncResult);
				//Console.WriteLine(bytesRead);

				// Got some data, need to read more
				if (bytesRead > 0)
				{
					// Report some progress, including total # bytes read, % complete, and transfer rate
					reqState.bytesRead += bytesRead;
					//double pctComplete = ((double)reqState.bytesRead / (double)reqState.totalBytes) * 100.0f;

					// Note: bytesRead/totalMS is in bytes/ms.  Convert to kb/sec.
					//TimeSpan totalTime = DateTime.Now - reqState.transferStart;
					//double kbPerSec = (reqState.bytesRead * 1000.0f) / (totalTime.TotalMilliseconds * 1024.0f);

					if (reqState.bufferData == null)
					{
						reqState.bufferData = new byte[bytesRead];
						System.Buffer.BlockCopy (reqState.bufferRead, 0, reqState.bufferData, 0, bytesRead);
					}
					else
					{
						byte[] rv = new byte[ bytesRead + reqState.bufferData.Length];
						System.Buffer.BlockCopy (reqState.bufferData, 0, rv, 0, reqState.bufferData.Length);
						System.Buffer.BlockCopy (reqState.bufferRead, 0, rv, reqState.bufferData.Length, bytesRead);

						reqState.bufferData = rv;
						rv = null;
					}

					//Console.WriteLine(Encoding.ASCII.GetString(reqState.bufferRead));
					//reqState.progCB(reqState.bytesRead, pctComplete, kbPerSec);

					// Kick off another read
					responseStream.BeginRead(reqState.bufferRead, 0, BUFFER_SIZE, new AsyncCallback(ReadCallback), reqState);
					return;
				}

				// EndRead returned 0, so no more data to be read
				else
				{
					responseStream.Close();
					reqState.response.Close();
					reqState.doneCB();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine (ex.Message);
			}
		}
	}
}

