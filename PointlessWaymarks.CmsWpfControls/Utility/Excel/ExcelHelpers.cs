using System.Diagnostics;
using System.IO;
using System.Linq.Dynamic.Core;
using ClosedXML.Excel;
using Omu.ValueInjecter;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsData.ExcelImport;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.CmsWpfControls.Utility.Excel;

public static class ExcelHelpers
{
    public static FileInfo ContentToExcelFileAsTable(List<object> toDisplay, string fileName,
        bool openAfterSaving = true, bool limitRowHeight = true, IProgress<string>? progress = null)
    {
        progress?.Report($"Starting transfer of {toDisplay.Count} to Excel");
        
        var file = Path.Combine(FileLocationTools.TempStorageDirectory().FullName,
            $"{DateTime.Now:yyyy-MM-dd--HH-mm-ss}---{FileAndFolderTools.TryMakeFilenameValid(fileName)}.xlsx");
        
        return ExcelTools.ToExcelFileAsTable(toDisplay, file, openAfterSaving, limitRowHeight, progress);
    }
    
    public static async Task ImportFromExcelFile(StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();
        
        statusContext.Progress("Starting Excel File import.");
        
        var dialog = new VistaOpenFileDialog();
        
        if (!(dialog.ShowDialog() ?? false)) return;
        
        var newFile = new FileInfo(dialog.FileName);
        
        if (!newFile.Exists)
        {
            await statusContext.ToastError("File doesn't exist?");
            return;
        }
        
        ContentImport.ContentImportResults contentImportResult;
        
        try
        {
            contentImportResult =
                await ContentImport.ImportFromFile(newFile.FullName, statusContext.ProgressTracker());
        }
        catch (Exception e)
        {
            await statusContext.ShowMessageWithOkButton("Import File Errors",
                $"Import Stopped because of an error processing the file:{Environment.NewLine}{e.Message}");
            return;
        }
        
        if (contentImportResult.HasError)
        {
            await statusContext.ShowMessageWithOkButton("Import Errors",
                $"Import Stopped because errors were reported:{Environment.NewLine}{contentImportResult.ErrorNotes}");
            return;
        }
        
        var shouldContinue = await statusContext.ShowMessage("Confirm Import",
            $"Continue?{Environment.NewLine}{Environment.NewLine}{contentImportResult.ToUpdate.Count} updates from {newFile.FullName} {Environment.NewLine}" +
            $"{string.Join(Environment.NewLine, contentImportResult.ToUpdate.Select(x => $"{Environment.NewLine}{x.Title}{Environment.NewLine}{x.DifferenceNotes}"))}",
            ["Yes", "No"]);
        
        if (shouldContinue == "No") return;
        
        var saveResult =
            await ContentImport.SaveAndGenerateHtmlFromExcelImport(contentImportResult,
                statusContext.ProgressTracker());
        
        if (saveResult.hasError)
        {
            await statusContext.ShowMessageWithOkButton("Excel Import Save Errors",
                $"There were error saving changes from the Excel Content:{Environment.NewLine}{saveResult.errorMessage}");
            return;
        }
        
        await statusContext.ToastSuccess(
            $"Imported {contentImportResult.ToUpdate.Count} items with changes from {newFile.FullName}");
    }
    
    public static async Task ImportFromOpenExcelInstance(StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();
        
        statusContext.Progress("Starting Excel Open Instance import.");
        
        ContentImport.ContentImportResults contentImportResult;
        
        try
        {
            contentImportResult =
                await ContentImport.ImportFromTopMostExcelInstance(statusContext.ProgressTracker());
        }
        catch (Exception e)
        {
            await statusContext.ShowMessageWithOkButton("Import Errors",
                $"Import Stopped because of an error processing the file:{Environment.NewLine}{Environment.NewLine}{e.Message}");
            return;
        }
        
        if (contentImportResult.HasError)
        {
            await statusContext.ShowMessageWithOkButton("Import Errors",
                $"Import Stopped because errors were reported:{Environment.NewLine}{Environment.NewLine}{contentImportResult.ErrorNotes}");
            return;
        }
        
        var shouldContinue = await statusContext.ShowMessage("Confirm Import",
            $"Continue?{Environment.NewLine}{Environment.NewLine}{contentImportResult.ToUpdate.Count} updates from Excel {Environment.NewLine}" +
            $"{string.Join(Environment.NewLine, contentImportResult.ToUpdate.Select(x => $"{Environment.NewLine}{x.Title}{Environment.NewLine}{x.DifferenceNotes}"))}",
            ["Yes", "No"]);
        
        if (shouldContinue == "No") return;
        
        var saveResult =
            await ContentImport.SaveAndGenerateHtmlFromExcelImport(contentImportResult,
                statusContext.ProgressTracker());
        
        if (saveResult.hasError)
        {
            await statusContext.ShowMessageWithOkButton("Excel Import Save Errors",
                $"There were error saving changes from the Excel Content:{Environment.NewLine}{saveResult.errorMessage}");
            return;
        }
        
        await statusContext.ToastSuccess($"Imported {contentImportResult.ToUpdate.Count} items with changes from Excel");
    }
    
    public static async Task<FileInfo?> PointContentToExcel(List<Guid> toDisplay, string fileName,
        bool openAfterSaving = true, IProgress<string>? progress = null)
    {
        var pointsAndDetails = await Db.PointsAndPointDetails(toDisplay);
        
        return PointContentToExcel(pointsAndDetails, fileName, openAfterSaving, progress);
    }
    
    public static FileInfo? PointContentToExcel(List<PointContentDto> toDisplay, string fileName,
        bool openAfterSaving = true, IProgress<string>? progress = null)
    {
        if (!toDisplay.Any()) return null;
        
        progress?.Report("Setting up list to transfer to Excel");
        
        var transformedList = toDisplay.Select(x => PointContent.CreateInstance().InjectFrom(x)).Cast<PointContent>()
            .ToList();
        
        var detailList = new List<(Guid, string)>();
        
        foreach (var loopContent in toDisplay)
        {
            progress?.Report($"Processing {loopContent.Title} with {loopContent.PointDetails.Count} details");
            // ! This content format is used by ContentImport !
            // Push the content into a compromise format that is ok for human generation (the target here is not creating 'by
            //  hand in Excel' rather taking something like GNIS data and concatenating/text manipulating the data into
            //  shape) and still ok for parsing in code
            foreach (var loopDetail in loopContent.PointDetails)
                detailList.Add((loopContent.ContentId,
                    $"ContentId:{loopDetail.ContentId}||{Environment.NewLine}Type:{loopDetail.DataType}||{Environment.NewLine}Data:{loopDetail.StructuredDataAsJson}"));
        }
        
        var file = new FileInfo(Path.Combine(FileLocationTools.TempStorageDirectory().FullName,
            $"{DateTime.Now:yyyy-MM-dd--HH-mm-ss}---{FileAndFolderTools.TryMakeFilenameValid(fileName)}.xlsx"));
        
        progress?.Report($"File Name {file.FullName} - creating Excel Workbook");
        
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Exported Data");
        
        progress?.Report("Inserting Content Data");
        
        var insertedTable = ws.Cell(1, 1).InsertTable(transformedList);
        
        progress?.Report("Adding Detail Columns...");
        
        var contentIdColumn = insertedTable.Row(1).Cells().Single(x => x.GetString() == "ContentId")
            .WorksheetColumn().ColumnNumber();
        
        //Create columns to the right of the existing table to hold the Point Details and expand the table
        var neededDetailColumns = detailList.GroupBy(x => x.Item1).Max(x => x.Count());
        
        var firstDetailColumn = insertedTable.Columns().Last().WorksheetColumn().ColumnNumber() + 1;
        
        for (var i = firstDetailColumn; i < firstDetailColumn + neededDetailColumns; i++)
            ws.Cell(1, i).Value = $"PointDetail {i - firstDetailColumn + 1}";
        
        if (neededDetailColumns > 0) insertedTable.Resize(ws.RangeUsed());
        
        //Match in the point details (match rather than assume list/excel ordering)
        foreach (var loopRow in insertedTable.Rows().Skip(1))
        {
            var rowContentId = Guid.Parse(loopRow.Cell(contentIdColumn).GetString());
            var matchedData = detailList.Where(x => x.Item1 == rowContentId);
            
            var currentColumn = firstDetailColumn;
            
            foreach (var loopDetail in matchedData)
            {
                loopRow.Cell(currentColumn).Value = loopDetail.Item2;
                currentColumn++;
            }
        }
        
        progress?.Report("Applying Formatting");
        
        //Format
        ws.Columns().AdjustToContents();
        
        foreach (var loopColumn in ws.ColumnsUsed().Where(x => x.Width > 70))
        {
            loopColumn.Width = 70;
            loopColumn.Style.Alignment.WrapText = true;
        }
        
        ws.Rows().AdjustToContents();
        
        foreach (var loopRow in ws.RowsUsed().Where(x => x.Height > 100)) loopRow.Height = 100;
        
        progress?.Report($"Saving Excel File {file.FullName}");
        
        wb.SaveAs(file.FullName);
        
        if (openAfterSaving)
        {
            progress?.Report($"Opening Excel File {file.FullName}");
            
            var ps = new ProcessStartInfo(file.FullName) { UseShellExecute = true, Verb = "open" };
            Process.Start(ps);
        }
        
        return file;
    }
    
    public static async Task SelectedToExcel(List<dynamic>? selected, StatusControlContext? statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();
        
        if (selected == null || !selected.Any())
        {
            statusContext?.ToastError("Nothing to send to Excel?");
            return;
        }
        
        List<object> excelObjects = [];
        
        var firstType = ((object)selected.First().DbEntry).GetType();
        
        if (selected.All(x => x.DbEntry is LineContent))
        {
            excelObjects.AddRange(selected.Where(x => x.DbEntry is LineContent).Select(x => x.DbEntry as LineContent)
                .Select(x => new LineContentForExcel().InjectFrom(x)));
        }
        else if (selected.All(x => x.DbEntry is GeoJsonContent))
        {
            excelObjects.AddRange(selected.Where(x => x.DbEntry is GeoJsonContent)
                .Select(x => x.DbEntry as GeoJsonContent)
                .Select(x => new GeoJsonContentForExcel().InjectFrom(x)));
        }
        else if (selected.All(x => x.DbEntry is PointContentDto))
        {
            var listObjects = selected.Where(x => x.DbEntry is PointContentDto)
                .Select(x => x.DbEntry as PointContentDto).Cast<PointContentDto>().ToList();
            
            foreach (var loopListObjects in listObjects)
            {
                //2024/4/15 - For semi-reasonable Excel output Point Details are translated into columns.
                //The obvious way to do that would be via an ExpandoObject - but ClosedXML renders
                //an Expando as a dictionary and doesn't understand the dynamic properties as columns...
                //Because the ClosedXML InsertTable is so useful for format and data rather than trying
                //to re-implement that functionality this code uses Dynamic LINQ (core) to build a class
                //at runtime.
                
                //TODO: Point Details would be nicer in Excel with rendered escape characters
                
                var basePointForExcel = new PointContentDtoForExcel();
                basePointForExcel.InjectFrom(loopListObjects);
                
                List<DynamicProperty> props = [];
                
                props.AddRange(typeof(PointContentDtoForExcel).GetProperties()
                    .Where(x => !x.Name.Equals("PointDetails", StringComparison.OrdinalIgnoreCase))
                    .Select(propertyInfo => new DynamicProperty(propertyInfo.Name, propertyInfo.PropertyType)));
                
                var detailColumnAndValueList = new List<(string, string)>();
                var detailColumnCounter = 0;
                foreach (var toAdd in loopListObjects.PointDetails)
                {
                    detailColumnAndValueList.Add(($"PointDetail{++detailColumnCounter}",
                        $"ContentId:{toAdd.ContentId}||Type:{toAdd.DataType}||Data:{toAdd.StructuredDataAsJson}"));
                    props.Add(new DynamicProperty($"PointDetail{detailColumnCounter}", typeof(string)));
                }
                
                var excelRowObjectType = DynamicClassFactory.CreateType(props);
                var excelRowObject = Activator.CreateInstance(excelRowObjectType) as DynamicClass;
                
                excelRowObject.InjectFrom(basePointForExcel);
                
                foreach (var loopDetails in detailColumnAndValueList)
                    excelRowObject!.SetDynamicPropertyValue(loopDetails.Item1, loopDetails.Item2);
                
                excelObjects.Add(excelRowObject!);
            }
        }
        else if (selected.All(x => ((object)x.DbEntry).GetType() == firstType))
        {
            excelObjects.AddRange(selected
                .Select(x => x.DbEntry).Cast<object>().ToList());
        }
        else
        {
            excelObjects.AddRange(selected.Select(x =>
                StaticValueInjecter.InjectFrom(new ContentCommonShell(), x.DbEntry)));
        }
        
        ContentToExcelFileAsTable(excelObjects, "SelectedItems",
            progress: statusContext?.ProgressTracker());
    }
}