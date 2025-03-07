using System.Net;
using System.Text;
using Amazon.S3.Transfer;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.CommonHtml;
using PointlessWaymarks.CmsData.S3;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.CommonTools.S3;
using PointlessWaymarks.WindowsTools;
using Polly;
using Serilog;

namespace PointlessWaymarks.CmsTask.PublishSiteToS3;

public class PublishToS3
{
    public async Task Publish(PublishToS3Settings settings)
    {
        var notifier = (await WindowsNotificationBuilders.NewNotifier(PublishToS3Settings.ProgramShortName()))
            .SetAutomationLogoNotificationIconUrl().SetErrorReportAdditionalInformationMarkdown(
                EmbeddedResourceTools.GetEmbeddedResourceText("README.md"));

        var consoleProgress = new ConsoleProgress();

        UserSettingsUtilities.SettingsFileFullName = settings.PointlessWaymarksSiteSettingsFileFullName;
        var siteSettings = await UserSettingsUtilities.ReadFromCurrentSettingsFile(consoleProgress);
        siteSettings.VerifyOrCreateAllTopLevelFolders();

        await UserSettingsUtilities.EnsureDbIsPresent(consoleProgress);

        await SiteGeneration.ChangedSiteContent(consoleProgress);
        var toUpload = await S3CmsTools.FilesSinceLastUploadToUploadList(consoleProgress);

        if (!toUpload.validUploadList.Valid)
        {
            Log.Error(
                $"Upload Failure - Generating HTML appears to have succeeded but creating an upload failed: {toUpload.validUploadList.Explanation}");
            await notifier.Error("Upload Failure",
                $"Generating HTML appears to have succeeded but creating an upload failed: {toUpload.validUploadList.Explanation}");
            return;
        }

        await S3CmsTools.S3UploaderItemsToS3UploaderJsonFile(toUpload.uploadItems,
            Path.Combine(UserSettingsSingleton.CurrentSettings().LocalScriptsDirectory().FullName,
                $"{DateTime.Now:yyyy-MM-dd--HH-mm-ss}---File-Upload-Data.json"));

        var s3Client = S3CmsTools.S3AccountInformationFromSettings().S3Client();

        var fileTransferUtility = new TransferUtility(s3Client);

        var progressList = new List<(bool sucess, S3UploadRequest uploadRequest)>();
        var exceptionList = new List<Exception>();

        var progressCount = 0;

        foreach (var loopUpload in toUpload.uploadItems)
        {
            if (progressCount++ % 10 == 0)
                consoleProgress.Report($"   S3 Upload Progress - {progressCount} of {toUpload.uploadItems.Count}");

            var uploadRequest = loopUpload.UploadRequestFilePath();

            try
            {
                await Policy.Handle<Exception>()
                    .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(2 * (retryAttempt + 1)),
                        (exception, _, retryCount, _) =>
                        {
                            Log.ForContext("exception", exception.ToString()).Verbose(exception,
                                "S3 Upload Retry {retryCount} - {exceptionMessage}", retryCount,
                                exception.Message);

                            if (exception is IOException && retryCount == 1)
                            {
                                uploadRequest = loopUpload.UploadRequestStream();

                                if (S3CmsTools.S3AccountInformationFromSettings().S3Provider() ==
                                    S3Providers.Cloudflare) uploadRequest.DisablePayloadSigning = true;
                            }
                        }).ExecuteAsync(() => fileTransferUtility.UploadAsync(uploadRequest));

                Log.Verbose($"S3 Upload Completed - {loopUpload.ToUpload.LocalFile.FullName} to {loopUpload.S3Key}");
                progressList.Add((true, loopUpload));
            }
            catch (Exception e)
            {
                exceptionList.Add(e);
                progressList.Add((false, loopUpload));
                Log.ForContext("loopUpload", loopUpload.SafeObjectDump()).Error(e,
                    $"S3 Upload Failed - {loopUpload.ToUpload.LocalFile.FullName} to {loopUpload.S3Key}");
            }
        }

        if (progressList.All(x => x.sucess))
        {
            notifier.Message(
                $"{UserSettingsSingleton.CurrentSettings().SiteName} Published! {progressList.Count} Files Uploaded to S3.");
        }
        else
        {
            var failures = progressList.Where(x => !x.sucess).Select(x => x.uploadRequest).ToList();
            var successList = progressList.Where(x => x.sucess).Select(x => x.uploadRequest).ToList();


            var failureFile = Path.Combine(UserSettingsSingleton.CurrentSettings().LocalScriptsDirectory().FullName,
                $"{DateTime.Now:yyyy-MM-dd--HH-mm-ss}---Upload-Failures.json");

            await S3CmsTools.S3UploaderItemsToS3UploaderJsonFile(failures, failureFile);

            var failureBody = new StringBuilder();
            failureBody.AppendLine(
                $"<p>{failures.Count} Upload Failed - {successList.Count} Uploads Succeeded. This can leave a site in an unpredictable state with a mix of old and new files/content...</p>");
            failureBody.AppendLine(
                "<p>The failures are listed below and have also been saved as a file that you can open in the Pointless Waymarks CMS -- File Log Tab, Written Files Tab, S3 Menu -> Open Uploader Json File -- and try Uploading these files again.</p>");
            failureBody.AppendLine($"<p>{failureFile}</p>");

            failureBody.AppendLine("<p>Failed Uploads:<p>");
            failureBody.AppendLine("<ul>");

            failures.ForEach(x =>
                failureBody.AppendLine($"<li>{WebUtility.HtmlEncode(x.ToUpload.LocalFile.FullName)}</li>"));
            failureBody.AppendLine("</ul>");

            failureBody.AppendLine("<br><p>Successful Uploads:<p>");
            failureBody.AppendLine("<ul>");
            successList.ForEach(x =>
                failureBody.AppendLine($"<li>{WebUtility.HtmlEncode(x.ToUpload.LocalFile.FullName)}</li>"));
            failureBody.AppendLine("</ul>");


            failureBody.AppendLine(
                exceptionList.Count > 10 ? "<br><p>First 10 Exceptions:<p>" : "<br><p>Exceptions:<p>");
            failureBody.AppendLine("<br><p>Exceptions<p>");
            failureBody.AppendLine("<ul>");
            exceptionList.Take(10).ToList()
                .ForEach(x => failureBody.AppendLine($"<li>{WebUtility.HtmlEncode(x.Message)}</li>"));
            failureBody.AppendLine("</ul>");

            await notifier.Error($"{siteSettings.SiteName} Upload Failure", failureBody.ToString());
        }
    }
}