﻿/*
 * (C) 2010 Kees van den Broek: kvdb@kvdb.net
 *          D-centralize: d-centralize.nl
 *          
 * Latest version and examples on: http://kvdb.net/projects/webdav
 * 
 * Feel free to use this code however you like.
 * http://creativecommons.org/license/zero/
 * 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
// for SSL certificate validation
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace Tomboy.PrivateNotes
{
  public delegate void ListCompleteDel(List<String> files, int statusCode);
  public delegate void UploadCompleteDel(int statusCode, object state);
  public delegate void DownloadCompleteDel(int statusCode);
  public delegate void CreateDirCompleteDel(int statusCode);
  public delegate void DeleteCompleteDel(int statusCode);

	/// <summary>
	/// takes care of communicating with a webdav server
	/// not note-sync specific, but general-purpose
	/// it is used by the WebDAVInterface
	/// </summary>
  public class WebDAVClient
  {
    public event ListCompleteDel ListComplete;
    public event UploadCompleteDel UploadComplete;
    public event DownloadCompleteDel DownloadComplete;
    public event CreateDirCompleteDel CreateDirComplete;
    public event DeleteCompleteDel DeleteComplete;

    public const int EXCEPTION_RESPONSE_CODE = -1;

    //XXX: submit along with state object.
    HttpWebRequest httpWebRequest;

    private bool verifyCert = true;
    public bool SSLCertVerification
    {
      get { return verifyCert; }
      set { verifyCert = value; }
    }

    private Exception lastError = null;
    public Exception LastException
    {
      get { return lastError; }
    }

    public void ResetException()
    {
      lastError = null;
    }


    #region WebDAV connection parameters
    private String server;
    /// <summary>
    /// Specify the WebDAV hostname (required).
    /// </summary>
    public String Server
    {
      get { return server; }
      set
      {
        value = value.TrimEnd('/');
        server = value;
      }
    }
    private String basePath = "/";
    /// <summary>
    /// Specify the path of a WebDAV directory to use as 'root' (default: /)
    /// </summary>
    public String BasePath
    {
      get { return basePath; }
      set
      {
        value = value.Trim('/');
        basePath = "/" + value + "/";
      }
    }
    private int? port = null;
    /// <summary>
    /// Specify an port (default: null = auto-detect)
    /// </summary>
    public int? Port
    {
      get { return port; }
      set { port = value; }
    }
    private String user;
    /// <summary>
    /// Specify a username (optional)
    /// </summary>
    public String User
    {
      get { return user; }
      set { user = value; }
    }
    private String pass;
    /// <summary>
    /// Specify a password (optional)
    /// </summary>
    public String Pass
    {
      get { return pass; }
      set { pass = value; }
    }
    private String domain = null;
    public String Domain
    {
      get { return domain; }
      set { domain = value; }
    }

    Uri getServerUrl(String path, Boolean appendTrailingSlash)
    {
      String completePath = basePath;
      if (path != null)
      {
        completePath += path.Trim('/');
      }

      if (appendTrailingSlash && completePath.EndsWith("/") == false) { completePath += '/'; }

      if (port.HasValue)
      {
        return new Uri(server + ":" + port + completePath);
      }
      else
      {
        return new Uri(server + completePath);
      }

    }
    #endregion

    #region WebDAV operations
    /// <summary>
    /// List files in the root directory
    /// </summary>
    public void List()
    {
      // Set default depth to 1. This would prevent recursion (default is infinity).
      List("/", 1);
    }

    /// <summary>
    /// List files in the given directory
    /// </summary>
    /// <param name="path"></param>
    public void List(String path)
    {
      // Set default depth to 1. This would prevent recursion.
      List(path, 1);
    }

    /// <summary>
    /// List all files present on the server.
    /// </summary>
    /// <param name="remoteFilePath">List only files in this path</param>
    /// <param name="depth">Recursion depth</param>
    /// <returns>A list of files (entries without a trailing slash) and directories (entries with a trailing slash)</returns>
    public void List(String remoteFilePath, int? depth)
    {
      // Uri should end with a trailing slash
      Uri listUri = getServerUrl(remoteFilePath, true);

      // http://webdav.org/specs/rfc4918.html#METHOD_PROPFIND
      StringBuilder propfind = new StringBuilder();
      propfind.Append("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
      propfind.Append("<propfind xmlns=\"DAV:\">");
      propfind.Append("  <propname/>");
      propfind.Append("</propfind>");

      // Depth header: http://webdav.org/specs/rfc4918.html#rfc.section.9.1.4
      IDictionary<string, string> headers = new Dictionary<string, string>();
      if (depth != null)
      {
        headers.Add("Depth", depth.ToString());
      }

      AsyncCallback callback = new AsyncCallback(FinishList);
      HTTPRequest(listUri, "PROPFIND", headers, Encoding.UTF8.GetBytes(propfind.ToString()), null, callback, remoteFilePath);
    }


    void FinishList(IAsyncResult result)
    {
      // CHECK FOR EXCEPTION FIRST!
      if (lastError != null)
      {
        Console.WriteLine("exception occured");
        if (ListComplete != null)
        {
          ListComplete(null, EXCEPTION_RESPONSE_CODE);
        }
        return;
      }
      // END EXCEPTION CODE

      string remoteFilePath = (string)result.AsyncState;
      int statusCode = 0;
      List<String> files = new List<string>();
      try
      {
        using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.EndGetResponse(result))
        {
          statusCode = (int)response.StatusCode;
          using (Stream stream = response.GetResponseStream())
          {
            XmlDocument xml = new XmlDocument();
            xml.Load(stream);
            XmlNamespaceManager xmlNsManager = new XmlNamespaceManager(xml.NameTable);
            xmlNsManager.AddNamespace("d", "DAV:");

            foreach (XmlNode node in xml.DocumentElement.ChildNodes)
            {
              XmlNode xmlNode = node.SelectSingleNode("d:href", xmlNsManager);
              string filepath = Uri.UnescapeDataString(xmlNode.InnerXml);
              string[] file = filepath.Split(new string[1] { basePath }, 2, StringSplitOptions.RemoveEmptyEntries);
              if (file.Length > 0)
              {
                // Want to see directory contents, not the directory itself.
                if (file[file.Length - 1] == remoteFilePath || file[file.Length - 1] == server) { continue; }
                files.Add(file[file.Length - 1]);
              }
            }
          }
        }
      }
      catch (Exception _e)
      {
        // new error handler
        lastError = _e;
        Logger.Warn("Exception: " + _e.Message + "\n" + _e.StackTrace + "\n----------------");
        if (ListComplete != null)
        {
          ListComplete(null, EXCEPTION_RESPONSE_CODE);
        }
      }

      if (ListComplete != null)
      {
        ListComplete(files, statusCode);
      }
    }

    /// <summary>
    /// Upload a file to the server
    /// </summary>
    /// <param name="localFilePath">Local path and filename of the file to upload</param>
    /// <param name="remoteFilePath">Destination path and filename of the file on the server</param>
    public void Upload(String localFilePath, String remoteFilePath)
    {
      Upload(localFilePath, remoteFilePath, null);
    }

    /// <summary>
    /// Upload a file to the server
    /// </summary>
    /// <param name="localFilePath">Local path and filename of the file to upload</param>
    /// <param name="remoteFilePath">Destination path and filename of the file on the server</param>
    /// <param name="state">Object to pass along with the callback</param>
    public void Upload(String localFilePath, String remoteFilePath, object state)
    {
      FileInfo fileInfo = new FileInfo(localFilePath);
      long fileSize = fileInfo.Length;

      Uri uploadUri = getServerUrl(remoteFilePath, false);
      string method = WebRequestMethods.Http.Put.ToString();

      AsyncCallback callback = new AsyncCallback(FinishUpload);
      HTTPRequest(uploadUri, method, null, null, localFilePath, callback, state);
    }


    void FinishUpload(IAsyncResult result)
    {
      // CHECK FOR EXCEPTION FIRST!
      if (lastError != null)
      {
        if (UploadComplete != null)
        {
          UploadComplete(EXCEPTION_RESPONSE_CODE, null);
        }
        return;
      }
      // END EXCEPTION CODE

      int statusCode = 0;

      try
      {
        using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.EndGetResponse(result))
        {
          statusCode = (int)response.StatusCode;
        }
      }
      catch (Exception _e)
      {
        // new error handler
        lastError = _e;
        Logger.Warn("Exception: " + _e.Message + "\n" + _e.StackTrace + "\n----------------");
        if (UploadComplete != null)
        {
          UploadComplete(EXCEPTION_RESPONSE_CODE, null);
        }
      }

      if (UploadComplete != null)
      {
        UploadComplete(statusCode, result.AsyncState);
      }
    }


    /// <summary>
    /// Download a file from the server
    /// </summary>
    /// <param name="remoteFilePath">Source path and filename of the file on the server</param>
    /// <param name="localFilePath">Destination path and filename of the file to download on the local filesystem</param>
    public void Download(String remoteFilePath, String localFilePath)
    {
      // Should not have a trailing slash.
      Uri downloadUri = getServerUrl(remoteFilePath, false);
      string method = WebRequestMethods.Http.Get.ToString();

      AsyncCallback callback = new AsyncCallback(FinishDownload);
      HTTPRequest(downloadUri, method, null, null, null, callback, localFilePath);
    }


    void FinishDownload(IAsyncResult result)
    {
      // CHECK FOR EXCEPTION FIRST!
      if (lastError != null)
      {
        if (DownloadComplete != null)
        {
          DownloadComplete(EXCEPTION_RESPONSE_CODE);
        }
        return;
      }
      // END EXCEPTION CODE

      string localFilePath = (string)result.AsyncState;
      int statusCode = 0;

      try
      {
        using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.EndGetResponse(result))
        {
          statusCode = (int)response.StatusCode;
          int contentLength = int.Parse(response.GetResponseHeader("Content-Length"));
          using (Stream s = response.GetResponseStream())
          {
            using (FileStream fs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
            {
              byte[] content = new byte[4096];
              int bytesRead = 0;
              do
              {
                bytesRead = s.Read(content, 0, content.Length);
                fs.Write(content, 0, bytesRead);
              } while (bytesRead > 0);
            }
          }
        }
      }
      catch (Exception _e)
      {
        // new error handler
        lastError = _e;
        Logger.Warn("Exception: " + _e.Message + "\n" + _e.StackTrace + "\n----------------");
        if (DownloadComplete != null)
        {
          DownloadComplete(EXCEPTION_RESPONSE_CODE);
        }
      }

      if (DownloadComplete != null)
      {
        DownloadComplete(statusCode);
      }
    }


    /// <summary>
    /// Create a directory on the server
    /// </summary>
    /// <param name="remotePath">Destination path of the directory on the server</param>
    public void CreateDir(string remotePath)
    {
      // Should not have a trailing slash.
      Uri dirUri = getServerUrl(remotePath, false);

      string method = WebRequestMethods.Http.MkCol.ToString();

      AsyncCallback callback = new AsyncCallback(FinishCreateDir);
      HTTPRequest(dirUri, method, null, null, null, callback, null);
    }


    void FinishCreateDir(IAsyncResult result)
    {
      // CHECK FOR EXCEPTION FIRST!
      if (lastError != null)
      {
        Console.WriteLine("exception occured");
        if (CreateDirComplete != null)
        {
          CreateDirComplete(EXCEPTION_RESPONSE_CODE);
        }
        return;
      }
      // END EXCEPTION CODE

      int statusCode = 0;

      try {
        using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.EndGetResponse(result))
        {
          statusCode = (int)response.StatusCode;
        }
      }
      catch (Exception _e)
      {
        // new error handler
        lastError = _e;
        Logger.Warn("Exception: " + _e.Message + "\n" + _e.StackTrace + "\n----------------");
        if (CreateDirComplete != null)
        {
          CreateDirComplete(EXCEPTION_RESPONSE_CODE);
        }
      }

      if (CreateDirComplete != null)
      {
        CreateDirComplete(statusCode);
      }
    }


    /// <summary>
    /// Delete a file on the server
    /// </summary>
    /// <param name="remoteFilePath"></param>
    public void Delete(string remoteFilePath)
    {
      Uri delUri = getServerUrl(remoteFilePath, remoteFilePath.EndsWith("/"));

      AsyncCallback callback = new AsyncCallback(FinishDelete);
      HTTPRequest(delUri, "DELETE", null, null, null, callback, null);
    }


    void FinishDelete(IAsyncResult result)
    {
      // CHECK FOR EXCEPTION FIRST!
      if (lastError != null)
      {
        if (DeleteComplete != null)
        {
          DeleteComplete(EXCEPTION_RESPONSE_CODE);
        }
        return;
      }
      // END EXCEPTION CODE

      int statusCode = 0;

      try
      {
        using (HttpWebResponse response = (HttpWebResponse)httpWebRequest.EndGetResponse(result))
        {
          statusCode = (int)response.StatusCode;
        }
      }
      catch (Exception _e)
      {
        // new error handler
        lastError = _e;
        Logger.Warn("Exception: " + _e.Message + "\n" + _e.StackTrace + "\n----------------");
        if (DeleteComplete != null)
        {
          DeleteComplete(EXCEPTION_RESPONSE_CODE);
        }
      }

      if (DeleteComplete != null)
      {
        DeleteComplete(statusCode);
      }
    }
    #endregion

    #region Server communication

    /// <summary>
    /// This class stores the request state of the request.
    /// </summary>
    public class RequestState
    {
      public WebRequest request;
      // The request either contains actual content...
      public byte[] content;
      // ...or a reference to the file to be added as content.
      public string uploadFilePath;
      // Callback and state to use after handling the request.
      public AsyncCallback userCallback;
      public object userState;
    }

    /// <summary>
    /// Perform the WebDAV call and fire the callback when finished.
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="requestMethod"></param>
    /// <param name="headers"></param>
    /// <param name="content"></param>
    /// <param name="uploadFilePath"></param>
    /// <param name="callback"></param>
    /// <param name="state"></param>
    void HTTPRequest(Uri uri, string requestMethod, IDictionary<string, string> headers, byte[] content, string uploadFilePath, AsyncCallback callback, object state)
    {
      httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(uri);

      /*
       * The following line fixes an authentication problem explained here:
       * http://www.devnewsgroups.net/dotnetframework/t9525-http-protocol-violation-long.aspx
       */
      System.Net.ServicePointManager.Expect100Continue = false;

      // If you want to disable SSL certificate validation
      if (SSLCertVerification == false)
      {
        System.Net.ServicePointManager.ServerCertificateValidationCallback +=
        delegate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors sslError)
        {
          bool validationResult = true;
          return validationResult;
        };
      }

      // The server may use authentication
      if (user != null && pass != null)
      {
        NetworkCredential networkCredential;
        if (domain != null)
        {
          networkCredential = new NetworkCredential(user, pass, domain);
        }
        else
        {
          networkCredential = new NetworkCredential(user, pass);
        }
        httpWebRequest.Credentials = networkCredential;
        // Send authentication along with first request.

        /// XXX WTF somehow if this is active, it doesn't work on linux?!
#if WIN32
        httpWebRequest.PreAuthenticate = true;
#endif
        /// END WTF
      }
      httpWebRequest.Method = requestMethod;

      // Need to send along headers?
      if (headers != null)
      {
        foreach (string key in headers.Keys)
        {
          httpWebRequest.Headers.Set(key, headers[key]);
        }
      }

      // Need to send along content?
      if (content != null || uploadFilePath != null)
      {
        RequestState asyncState = new RequestState();
        asyncState.request = httpWebRequest;
        asyncState.userCallback = callback;
        asyncState.userState = state;

        if (content != null)
        {
          // The request either contains actual content...
          httpWebRequest.ContentLength = content.Length;
          asyncState.content = content;
          httpWebRequest.ContentType = "text/xml";
        }
        else
        {
          // ...or a reference to the file to be added as content.
          httpWebRequest.ContentLength = new FileInfo(uploadFilePath).Length;
          asyncState.uploadFilePath = uploadFilePath;
        }

        // Perform asynchronous request.
        IAsyncResult r = (IAsyncResult)asyncState.request.BeginGetRequestStream(new AsyncCallback(ReadCallback), asyncState);
      }
      else
      {

        // Begin async communications
        httpWebRequest.BeginGetResponse(callback, state);
      }
    }

    /// <summary>
    /// Submit data asynchronously
    /// </summary>
    /// <param name="result"></param>
    private void ReadCallback(IAsyncResult result)
    {
      try {
      RequestState state = (RequestState)result.AsyncState;
      WebRequest request = state.request;

      // End the Asynchronus request.
      using (Stream streamResponse = request.EndGetRequestStream(result))
      {
        // Submit content
        if (state.content != null)
        {
          streamResponse.Write(state.content, 0, state.content.Length);
        }
        else
        {
          using (FileStream fs = new FileStream(state.uploadFilePath, FileMode.Open, FileAccess.Read))
          {
            byte[] content = new byte[4096];
            int bytesRead = 0;
            do
            {
              bytesRead = fs.Read(content, 0, content.Length);
              streamResponse.Write(content, 0, bytesRead);
            } while (bytesRead > 0);

            //XXX: perform upload status callback
          }
        }
      } 

      // Done, invoke user callback
      request.BeginGetResponse(state.userCallback, state.userState);
    } catch (Exception e) {
      lastError = e;
      RequestState state = (RequestState)result.AsyncState;
      state.userCallback.Invoke(result);
      Console.WriteLine("exception occured");
    }
    }
    #endregion
  }
}
