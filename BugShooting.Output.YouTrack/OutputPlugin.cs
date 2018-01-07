using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using BS.Plugin.V3.Output;
using BS.Plugin.V3.Common;
using BS.Plugin.V3.Utilities;

namespace BugShooting.Output.YouTrack
{
  public class OutputPlugin: OutputPlugin<Output>
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

    protected override OutputValues SerializeOutput(Output Output)
    {

      OutputValues outputValues = new OutputValues();

      outputValues.Add("Name", Output.Name);
      outputValues.Add("Url", Output.Url);
      outputValues.Add("UserName", Output.UserName);
      outputValues.Add("Password",Output.Password, true);
      outputValues.Add("OpenItemInBrowser", Convert.ToString(Output.OpenItemInBrowser));
      outputValues.Add("FileName", Output.FileName);
      outputValues.Add("FileFormat", Output.FileFormat);
      outputValues.Add("LastProjectID", Output.LastProjectID);
      outputValues.Add("LastIssueID", Output.LastIssueID);

      return outputValues;
      
    }

    protected override Output DeserializeOutput(OutputValues OutputValues)
    {

      return new Output(OutputValues["Name", this.Name],
                        OutputValues["Url", ""], 
                        OutputValues["UserName", ""],
                        OutputValues["Password", ""], 
                        OutputValues["FileName", "Screenshot"], 
                        OutputValues["FileFormat", ""],
                        Convert.ToBoolean(OutputValues["OpenItemInBrowser", Convert.ToString(true)]),
                        OutputValues["LastProjectID", string.Empty], 
                        OutputValues["LastIssueID", string.Empty]);

    }

    protected override async Task<SendResult> Send(IWin32Window Owner, Output Output, ImageData ImageData)
    {

      try
      {

        string userName = Output.UserName;
        string password = Output.Password;
        bool showLogin = string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password);
        bool rememberCredentials = false;

        string fileName = AttributeHelper.ReplaceAttributes(Output.FileName, ImageData);

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
              return new SendResult(Result.Canceled);
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
          Send send = new Send(Output.Url, Output.LastProjectID, Output.LastIssueID, projects, fileName);

          var sendOwnerHelper = new System.Windows.Interop.WindowInteropHelper(send);
          sendOwnerHelper.Owner = Owner.Handle;

          if (!send.ShowDialog() == true)
          {
            return new SendResult(Result.Canceled);
          }

          string projectID = null;
          string issueID = null;

          if (send.CreateNewIssue)
          {

            projectID = send.ProjectID;

            CreateIssueResult createIssueResult = await YouTrackProxy.CreateIssue(Output.Url, loginResult.LoginCookies, projectID, send.Summary, send.Description);
            if (!createIssueResult.Success)
            {
              return new SendResult(Result.Failed, createIssueResult.FaultMessage);
            }

            issueID = createIssueResult.IssueID;

          }
          else
          {
            issueID = send.IssueID;
            projectID = Output.LastProjectID;
          }

          string fullFileName = String.Format("{0}.{1}", send.FileName, FileHelper.GetFileExtension(Output.FileFormat));
          string fileMimeType = FileHelper.GetMimeType(Output.FileFormat);
          byte[] fileBytes = FileHelper.GetFileBytes(Output.FileFormat, ImageData);

          AddAttachmentResult addAttachmentResult = await YouTrackProxy.AddAttachment(Output.Url, loginResult.LoginCookies, issueID, fullFileName, fileBytes, fileMimeType);
          if (!addAttachmentResult.Success)
          {
            return new SendResult(Result.Failed, addAttachmentResult.FaultMessage);
          }

          // Open issue in browser
          if (Output.OpenItemInBrowser)
          {
            WebHelper.OpenUrl(String.Format("{0}/issue/{1}", Output.Url, issueID));
          }


          return new SendResult(Result.Success,
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
        return new SendResult(Result.Failed, ex.Message);
      }

    }

  }
}
