using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace WhatInsides.Models;

public class AttachmentItem : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _fileName = string.Empty;
    private long _fileSizeBytes;
    private string _encryptedFileName = string.Empty;
    private DateTime _createdAt = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string Id
    {
        get => _id;
        set { if (_id != value) { _id = value; OnPropertyChanged(); } }
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            if (_fileName != value)
            {
                _fileName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Extension));
                OnPropertyChanged(nameof(CategoryIconResourceKey));
            }
        }
    }

    public long FileSizeBytes
    {
        get => _fileSizeBytes;
        set
        {
            if (_fileSizeBytes != value)
            {
                _fileSizeBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedSize));
            }
        }
    }

    public string EncryptedFileName
    {
        get => _encryptedFileName;
        set { if (_encryptedFileName != value) { _encryptedFileName = value; OnPropertyChanged(); } }
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set { if (_createdAt != value) { _createdAt = value; OnPropertyChanged(); } }
    }

    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

    public string FormattedSize
    {
        get
        {
            if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024) return $"{(FileSizeBytes / 1024.0):F1} KB";
            if (FileSizeBytes < 1024 * 1024 * 1024) return $"{(FileSizeBytes / (1024.0 * 1024)):F1} MB";
            return $"{(FileSizeBytes / (1024.0 * 1024 * 1024)):F2} GB";
        }
    }

    public string CategoryIconResourceKey => Extension switch
    {
        ".pdf" => "IconPdf",
        ".key" or ".pem" or ".pub" or ".crt" or ".cer" or ".pfx" or ".p12" or ".asc" => "IconKey",
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" => "IconGridView",
        ".txt" or ".md" or ".json" or ".xml" or ".csv" or ".yml" or ".yaml" or ".env" or ".ini" or ".conf" => "IconDescription",
        _ => "IconFile"
    };
}
