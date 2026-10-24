using System;
using System.Collections.Generic;
using System.IO;

namespace WhatInsides.Services;

public class CloudProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDetected { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
}

public static class CloudDetectionService
{
    public static List<CloudProviderInfo> DetectAvailableProviders()
    {
        var list = new List<CloudProviderInfo>();

        // 1. Detect Google Drive (Primary Cloud Provider)
        string? googleDrivePath = null;
        try
        {
            // Check all mounted drives for Google Drive root ("My Drive" or "Drive Saya")
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string myDriveSub = Path.Combine(drive.RootDirectory.FullName, "My Drive");
                    string driveSayaSub = Path.Combine(drive.RootDirectory.FullName, "Drive Saya");

                    if (Directory.Exists(myDriveSub))
                    {
                        googleDrivePath = myDriveSub;
                        break;
                    }
                    if (Directory.Exists(driveSayaSub))
                    {
                        googleDrivePath = driveSayaSub;
                        break;
                    }
                    if (drive.VolumeLabel.Contains("Google Drive", StringComparison.OrdinalIgnoreCase))
                    {
                        googleDrivePath = drive.RootDirectory.FullName;
                        break;
                    }
                }
            }
        }
        catch { }

        // Fallback: Check user profile folder for Google Drive mirror
        if (string.IsNullOrEmpty(googleDrivePath))
        {
            string userGdrive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Google Drive");
            string userMyDrive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "My Drive");
            if (Directory.Exists(userGdrive))
            {
                googleDrivePath = userGdrive;
            }
            else if (Directory.Exists(userMyDrive))
            {
                googleDrivePath = userMyDrive;
            }
        }

        if (!string.IsNullOrEmpty(googleDrivePath) && Directory.Exists(googleDrivePath))
        {
            list.Add(new CloudProviderInfo
            {
                Name = "Google Drive",
                Path = googleDrivePath,
                IsDetected = true,
                Status = "Terdeteksi & Siap Sinkronisasi",
                AccountEmail = "user.vault@gmail.com"
            });
        }
        else
        {
            list.Add(new CloudProviderInfo
            {
                Name = "Google Drive",
                Path = string.Empty,
                IsDetected = false,
                Status = "Tidak terdeteksi (Google Drive untuk Desktop belum terpasang)",
                AccountEmail = string.Empty
            });
        }

        // 2. Detect Microsoft OneDrive (Secondary / Deferred)
        string? oneDrivePath = Environment.GetEnvironmentVariable("OneDrive")
            ?? Environment.GetEnvironmentVariable("OneDriveConsumer")
            ?? Environment.GetEnvironmentVariable("OneDriveCommercial");

        if (string.IsNullOrEmpty(oneDrivePath) || !Directory.Exists(oneDrivePath))
        {
            string defaultUserOneDrive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive");
            if (Directory.Exists(defaultUserOneDrive))
            {
                oneDrivePath = defaultUserOneDrive;
            }
        }

        if (!string.IsNullOrEmpty(oneDrivePath) && Directory.Exists(oneDrivePath))
        {
            list.Add(new CloudProviderInfo
            {
                Name = "OneDrive",
                Path = oneDrivePath,
                IsDetected = true,
                Status = "Tersedia (Pilihan Sekunder)",
                AccountEmail = "onedrive.vault@live.com"
            });
        }
        else
        {
            list.Add(new CloudProviderInfo
            {
                Name = "OneDrive",
                Path = string.Empty,
                IsDetected = false,
                Status = "Tidak terdeteksi",
                AccountEmail = string.Empty
            });
        }

        return list;
    }
}
