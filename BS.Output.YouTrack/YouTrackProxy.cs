using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace BS.Output.YouTrack
{
  internal class YouTrackProxy
  {

    static internal async Task<LoginResult> Login(string url, string userName, string password)
    {
      
      try
      {

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(String.Format("{0}/rest/user/login", url));
        request.CookieContainer = new CookieContainer();
        request.ContentType = "application/x-www-form-urlencoded";
        request.KeepAlive = true;
        request.Method = "POST";
        
        StringBuilder postData = new StringBuilder();
        postData.AppendFormat("login={0}&password={1}",userName, password);
        
        byte[] postBytes = Encoding.UTF8.GetBytes(postData.ToString());
        using (Stream requestStream = await request.GetRequestStreamAsync())
        {
          await requestStream.WriteAsync(postBytes, 0, postBytes.Length);
        }

        using (HttpWebResponse response = (HttpWebResponse) await request.GetResponseAsync())
        { 
          using (Stream responseStream = response.GetResponseStream())
          {
            using (StreamReader reader = new StreamReader(responseStream))
            {
              string responseData = reader.ReadToEnd();

              if (responseData.ToLower() == "<login>ok</login>")
              {
                return new LoginResult(true, response.Cookies);
              }
              else
              {
                return new LoginResult(true, null);
              }

            }
          }
        }

      }
      catch
      {
        return new LoginResult(false, null);
      }
    }

    static internal async Task<List<Project>> GetAccessibleProjects(string url, CookieCollection loginCookies)
    {

      HttpWebRequest request = (HttpWebRequest)WebRequest.Create(String.Format("{0}/rest/project/all", url));
      request.CookieContainer = new CookieContainer();
      request.KeepAlive = true;
      request.Method = "GET";

      request.CookieContainer = new CookieContainer();
      if ((loginCookies != null))
      {
        request.CookieContainer.Add(loginCookies);
      }

      string responseData = null;
      using (HttpWebResponse response = (HttpWebResponse) await request.GetResponseAsync())
      { 
        using (Stream responseStream = response.GetResponseStream())
        {
          using (StreamReader reader = new StreamReader(responseStream))
          {
            responseData = reader.ReadToEnd();
          }
        }
      }
      
      XmlDocument xmlDoc = new XmlDocument();
      xmlDoc.LoadXml(responseData);

      List<Project> projects = new List<Project>();

      foreach (XmlNode node in xmlDoc.GetElementsByTagName("project"))
      {
        projects.Add(new Project(node.Attributes["shortName"].Value, node.Attributes["name"].Value));
      }

      return projects;

    }

    static internal async Task<CreateIssueResult> CreateIssue(string url, CookieCollection loginCookies, string projectID, string summary, string description)
    {

      try
      {

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(String.Format("{0}/rest/issue?project={1}&summary={2}&description={3}", url, HttpUtility.UrlEncode(projectID), HttpUtility.UrlEncode(summary), HttpUtility.UrlEncode(description)));
        request.CookieContainer = new CookieContainer();
        request.Method = "PUT";
        
        request.CookieContainer = new CookieContainer();
        if ((loginCookies != null))
        {
          request.CookieContainer.Add(loginCookies);
        }

        // Add fake content, necessary for the YouTrack API
        byte[] contentBytes = Encoding.UTF8.GetBytes("--");
        request.ContentLength = contentBytes.Length;
        using (Stream requestStream = await request.GetRequestStreamAsync())
        {
          await requestStream.WriteAsync(contentBytes, 0, contentBytes.Length);
        }

        using (HttpWebResponse response = (HttpWebResponse) await request.GetResponseAsync())
        {
          string[] values = response.Headers["Location"].Split('/');
          return new CreateIssueResult(true, values[values.Length - 1], null);
        }

      }
      catch (WebException ex)
      {
        HttpWebResponse response = (HttpWebResponse)ex.Response;
        return new CreateIssueResult(false, null, response.StatusDescription);
      }
    }

    static internal async Task<AddAttachmentResult> AddAttachment(string url, CookieCollection loginCookies, string issueID, string fullFileName, byte[] fileBytes, string fileMimeType)
    {

      try
      {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(String.Format("{0}/rest/issue/{1}/attachment", url, issueID));

        string boundary = String.Format("----------{0}", DateTime.Now.Ticks.ToString("x"));

        request.CookieContainer = new CookieContainer();
        request.KeepAlive = true;
        request.ContentType = "multipart/form-data; boundary=" + boundary;
        request.Method = "POST";

        request.CookieContainer = new CookieContainer();
        if ((loginCookies != null))
        {
          request.CookieContainer.Add(loginCookies);
        }
        
        StringBuilder postData = new StringBuilder();
        postData.AppendFormat("--{0}", boundary);
        postData.AppendLine();
        postData.AppendFormat("Content-Disposition: form-data; name=\"{0}\"; filename=\"{0}\"\r\n", fullFileName);
        postData.AppendFormat("Content-Type: {0}\r\n", fileMimeType);
        postData.AppendFormat("Content-Transfer-Encoding: binary\r\n");
        postData.AppendLine();

        byte[] postBytes = Encoding.UTF8.GetBytes(postData.ToString());
        byte[] boundaryBytes = Encoding.ASCII.GetBytes(String.Format("\r\n--{0}--\r\n", boundary));

        request.ContentLength = postBytes.Length + fileBytes.Length + boundaryBytes.Length;

        using (Stream requestStream = await request.GetRequestStreamAsync())
        {
          requestStream.Write(postBytes, 0, postBytes.Length);
          requestStream.Write(fileBytes, 0, fileBytes.Length);
          requestStream.Write(boundaryBytes, 0, boundaryBytes.Length);
        }
        
        using (HttpWebResponse response = (HttpWebResponse) await request.GetResponseAsync())
        {
          return new AddAttachmentResult(true, null);
        }
        
      }
      catch (WebException ex)
      {
        HttpWebResponse response = (HttpWebResponse)ex.Response;
        return new AddAttachmentResult(false, response.StatusDescription);
      }
    }

  }

  internal class LoginResult
  {

    bool success;
    CookieCollection loginCookies;

    public LoginResult(bool success,
                       CookieCollection loginCookies)
    {
      this.success = success;
      this.loginCookies = loginCookies;
    }

    public bool Success
    {
      get { return success; }
    }

    public CookieCollection LoginCookies
    {
      get { return loginCookies; }
    }

  }

  internal class CreateIssueResult
  {

    bool success;
    string issueID;
    string faultMessage;

    public CreateIssueResult(bool success,
                             string issueID,
                             string faultMessage)
    {
      this.success = success;
      this.issueID = issueID;
      this.faultMessage = faultMessage;
    }

    public bool Success
    {
      get { return success; }
    }

    public string IssueID
    {
      get { return issueID; }
    }

    public string FaultMessage
    {
      get { return faultMessage; }
    }

  }

  internal class AddAttachmentResult
  {

    bool success;
    string faultMessage;

    public AddAttachmentResult(bool success,
                               string faultMessage)
    {
      this.success = success;
      this.faultMessage = faultMessage;
    }

    public bool Success
    {
      get { return success; }
    }

    public string FaultMessage
    {
      get { return faultMessage; }
    }

  }

  internal class Project
  {

    private string id;
    private string name;

    public Project(string id, string name)
    {
      this.id = id;
      this.name = name;
    }

    public string ID
    {
      get { return id; }
    }

    public string Name
    {
      get { return name; }
    }

  }

}
