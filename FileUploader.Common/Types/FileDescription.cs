﻿namespace FileUploader.Common.Types
{
    public class FileDescription
    {
        public string NameWithoutExtension { get; set; } 

        public string PhysicalPath { get; set; }

        public string RelativeLocation { get; set; }

        public string Extension { get; set; }

        public long Size { get; set; }
    }
}
