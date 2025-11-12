namespace TracesolCrossCheck_Upload_Service.Models;

public sealed class ItemLogRecord
{
    public long ID { get; set; }                 // bigint (not null)
    public DateTime DateTimeStamp { get; set; }   // datetime2(0) (not null)

    public string? SKU { get; set; }              // nvarchar(64) (null)
    public int? Pallet_Number { get; set; }       // int (null)
    public string? OCR_Description_1 { get; set; }// nvarchar(64) (null)
    public int? Quantity { get; set; }            // int (null)
    public string? Batch_Number { get; set; }     // nvarchar(64) (null)
    public string? Barcode { get; set; }          // nvarchar(64) (null)
    public string? OCR_Description_2 { get; set; }// nvarchar(64) (null)

    public bool Cross_Check { get; set; }         // bit (not null)
    public bool Label_Printed { get; set; }       // bit (not null)
    public bool Label_Applied { get; set; }       // bit (not null)

    public string? Check_Scan_Result { get; set; }// nvarchar(64) (null)

    public bool Valid { get; set; }               // bit (not null)
    public bool Sent { get; set; }                // bit (not null)
    public bool ImageSent { get; set; }           // bit (not null)
    public bool Duplicate { get; set; }           // bit (not null)
    public bool Complete { get; set; }            // bit (not null)
}
