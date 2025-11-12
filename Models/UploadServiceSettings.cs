using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TracesolCrossCheck_Upload_Service;

public sealed class UploadServiceSettings
{
    public string CsvOutputFolder { get; set; } = @"C:\Users\Public\Documents\TracesolCrossCheck\csv";
    public int IntervalMs { get; set; } = 500;
}
