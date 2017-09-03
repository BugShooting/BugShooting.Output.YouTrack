using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BS.Output.YouTrack
{
  public class OutputAddIn: V3.OutputAddIn<Output>
  {

    protected override string Name
    {
      get { return "YouTrack"; }
    }

    protected override Image Image64
    {
      get  { return Properties.Resources.logo_64; }
    }

    protected override Image Image16
    {
      get { return Properties.Resources.logo_16 ; }
    }

    protected override bool Editable
    {
      get { return true; }
    }

    protected override string Description
    {
      get { return "Attach screenshots to YouTrack issues."; }
    }
    
    protected override Output CreateOutput(IWin32Window Owner)
    {
      
      Output output = new Output(Name, 
                                 String.Empty, 
                                 String.Empty, 
                                 String.Empty, 
                                 "Screenshot",
                                 String.Empty, 
                                 true,
                                 String.Empty,
                                 String.Empty);

      return EditOutput(Owner, output);

    }

    protected override Output EditOutput(IWin32Window Owner, Output Output)
    {

      Edit edit = new Edit(Output);

      var ownerHelper = new System.Windows.Interop.WindowInteropHelper(edit);
      ownerHelper.Owner = Owner.Handle;
      
      if (edit.ShowDialog() == true) {

        return new Output(edit.OutputName,
                          edit.Url,
                          edit.UserName,
                          edit.Password,
                          edit.FileName,
                          edit.FileFormat,
                          edit.OpenItemInBrowser,
                          Output.LastProjectID,
                          Output.LastIssueID);
      }
      else
      {
        return null; 
      }

    }

    protected override OutputValueCollection SerializeOutput(Output Output)
    {

      OutputValueCollection outputValues = new OutputValueCollection();

      outputValues.Add(new OutputValue("Name", Output.Name));
      outputValues.Add(new OutputValue("Url", Output.Url));
      outputValues.Add(new OutputValue("UserName", Output.UserName));
      outputValues.Add(new OutputValue("Password",Output.Password, true));
      outputValues.Add(new OutputValue("OpenItemInBrowser", Convert.ToString(Output.OpenItemInBrowser)));
      outputValues.Add(new OutputValue("FileName", Output.FileName));
      outputValues.Add(new OutputValue("FileFormat", Output.FileFormat));
      outputValues.Add(new OutputValue("LastProjectID", Output.LastProjectID));
      outputValues.Add(new OutputValue("LastIssueID", Output.LastIssueID));

      return outputValues;
      
    }

    protected override Output DeserializeOutput(OutputValueCollection OutputValues)
    {

      return new Output(OutputValues["Name", this.Name].Value,
                        OutputValues["Url", ""].Value, 
                        OutputValues["UserName", ""].Value,
                        OutputValues["Password", ""].Value, 
                        OutputValues["FileName", "Screenshot"].Value, 
                        OutputValues["FileFormat", ""].Value,
                        Convert.ToBoolean(OutputValues["OpenItemInBrowser", Convert.ToString(true)].Value),
                        OutputValues["LastProjectID", string.Empty].Value, 
                        OutputValues["LastIssueID", string.Empty].Value);

    }

    protected override async Task<V3.SendResult> Send(IWin32Window Owner, Output Output, V3.ImageData ImageData)
    {

      try
      {

        string userName = Output.UserName;
        string password = Output.Password;
        bool showLogin = string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password);
        bool rememberCredentials = false;

        string fileName = V3.FileHelper.GetFileName(Output.FileName, Output.FileFormat, ImageData);

        while (true)
        {

          if (showLogin)
          {

            // Show credentials window
            Credentials credentials = new Credentials(Output.Url, userName, password, rememberCredentials);

            var credentialsOwnerHelper = new System.Windows.Interop.WindowInteropHelper(credentials);
            credentialsOwnerHelper.Owner = Owner.Handle;

            if (credentials.ShowDialog() != true)
            {
              return new V3.SendResult(V3.Result.Canceled);
            }

            userName = credentials.UserName;
            password = credentials.Password;
            rememberCredentials = credentials.Remember;

          }

          LoginResult loginResult = await YouTrackProxy.Login(Output.Url, userName, password);
          if (!loginResult.Success)
          {
            showLogin = true;
            continue;
          }

          // Get projects
          List<Project> projects = await YouTrackProxy.GetAccessibleProjects(Output.Url, loginResult.LoginCookies);

          // Show send window
          Send send = new Send(Output.Url, Output.LastProjectID, Output.LastIssueID, projects, userName, password, fileName);

          var sendOwnerHelper = new System.Windows.Interop.WindowInteropHelper(send);
          sendOwnerHelper.Owner = Owner.Handle;

          if (!send.ShowDialog() == true)
          {
            return new V3.SendResult(V3.Result.Canceled);
          }

          string projectID = null;
          string issueID = null;

          if (send.CreateNewIssue)
          {

            projectID = send.ProjectID;

            CreateIssueResult createIssueResult = await YouTrackProxy.CreateIssue(Output.Url, loginResult.LoginCookies, projectID, send.Summary, send.Description);
            if (!createIssueResult.Success)
            {
              return new V3.SendResult(V3.Result.Failed, createIssueResult.FaultMessage);
            }

            issueID = createIssueResult.IssueID;

          }
          else
          {
            issueID = send.IssueID;
            projectID = Output.LastProjectID;
          }

          string fullFileName = String.Format("{0}.{1}", send.FileName, V3.FileHelper.GetFileExtention(Output.FileFormat));
          string fileMimeType = V3.FileHelper.GetMimeType(Output.FileFormat);
          byte[] fileBytes = V3.FileHelper.GetFileBytes(Output.FileFormat, ImageData);

          AddAttachmentResult addAttachmentResult = await YouTrackProxy.AddAttachment(Output.Url, loginResult.LoginCookies, issueID, fullFileName, fileBytes, fileMimeType);
          if (!addAttachmentResult.Success)
          {
            return new V3.SendResult(V3.Result.Failed, addAttachmentResult.FaultMessage);
          }

          // Open issue in browser
          if (Output.OpenItemInBrowser)
          {
            V3.WebHelper.OpenUrl(String.Format("{0}/issue/{1}", Output.Url, issueID));
          }


          return new V3.SendResult(V3.Result.Success,
                                    new Output(Output.Name,
                                               Output.Url,
                                               (rememberCredentials) ? userName : Output.UserName,
                                               (rememberCredentials) ? password : Output.Password,
                                               Output.FileName,
                                               Output.FileFormat,
                                               Output.OpenItemInBrowser,
                                               projectID,
                                               issueID));
            
        }

      }
      catch (Exception ex)
      {
        return new V3.SendResult(V3.Result.Failed, ex.Message);
      }

    }

  }
}
