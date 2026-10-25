using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WhatInsides.Models;

namespace WhatInsides.Services;

public class CredentialVaultService
{
    private readonly string _storagePath;
    private readonly string _attachmentsDir;
    private readonly List<CredentialItem> _items = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CredentialVaultService(string? customStoragePath = null, string? customAttachmentsDir = null)
    {
        if (!string.IsNullOrWhiteSpace(customStoragePath))
        {
            _storagePath = customStoragePath;
            string? dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _attachmentsDir = !string.IsNullOrWhiteSpace(customAttachmentsDir)
                ? customAttachmentsDir
                : Path.Combine(dir ?? Path.GetTempPath(), "attachments");
        }
        else
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(localAppData, "WhatInsides");
            Directory.CreateDirectory(folder);
            _storagePath = Path.Combine(folder, "credentials_vault.json");
            _attachmentsDir = !string.IsNullOrWhiteSpace(customAttachmentsDir)
                ? customAttachmentsDir
                : Path.Combine(folder, "attachments");
        }

        Directory.CreateDirectory(_attachmentsDir);
        Load();
    }

    public IReadOnlyList<CredentialItem> GetAll() => _items.AsReadOnly();

    public CredentialItem? GetById(string id) => _items.FirstOrDefault(x => x.Id == id);

    public void Add(CredentialItem item)
    {
        item.UpdatedAt = DateTime.Now;
        _items.Insert(0, item);
        Save();
    }

    public void Update(CredentialItem item)
    {
        int index = _items.FindIndex(x => x.Id == item.Id);
        if (index >= 0)
        {
            item.UpdatedAt = DateTime.Now;
            _items[index] = item;
            Save();
        }
    }

    public void Delete(string id, bool permanent = false)
    {
        var item = GetById(id);
        if (item == null) return;

        if (permanent || item.IsDeleted)
        {
            foreach (var att in item.Attachments)
            {
                try
                {
                    string p = Path.Combine(_attachmentsDir, att.EncryptedFileName);
                    if (File.Exists(p)) File.Delete(p);
                }
                catch { }
            }
            _items.Remove(item);
        }
        else
        {
            item.IsDeleted = true;
            item.UpdatedAt = DateTime.Now;
        }
        Save();
    }

    public void Restore(string id)
    {
        var item = GetById(id);
        if (item != null)
        {
            item.IsDeleted = false;
            item.UpdatedAt = DateTime.Now;
            Save();
        }
    }

    public void ToggleFavorite(string id)
    {
        var item = GetById(id);
        if (item != null)
        {
            item.IsFavorite = !item.IsFavorite;
            item.UpdatedAt = DateTime.Now;
            Save();
        }
    }

    public string GetAttachmentsDirectory() => _attachmentsDir;

    public async Task<AttachmentItem> AddAttachmentAsync(
        string credentialId,
        string sourceFilePath,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Berkas lampiran tidak ditemukan.", sourceFilePath);

        var item = GetById(credentialId)
            ?? throw new InvalidOperationException($"Kredensial dengan ID '{credentialId}' tidak ditemukan.");

        string attachmentId = Guid.NewGuid().ToString("N");
        string encFileName = $"{attachmentId}.enc";
        string encFilePath = Path.Combine(_attachmentsDir, encFileName);

        using (var inStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var outStream = new FileStream(encFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await CryptoEngine.EncryptStreamAsync(inStream, outStream, password, null, cancellationToken);
        }

        var attachment = new AttachmentItem
        {
            Id = attachmentId,
            FileName = Path.GetFileName(sourceFilePath),
            FileSizeBytes = new FileInfo(sourceFilePath).Length,
            EncryptedFileName = encFileName,
            CreatedAt = DateTime.Now
        };

        item.Attachments.Add(attachment);
        item.UpdatedAt = DateTime.Now;
        Save();

        return attachment;
    }

    public async Task ExportAttachmentAsync(
        string credentialId,
        string attachmentId,
        string destinationFilePath,
        string password,
        CancellationToken cancellationToken = default)
    {
        var item = GetById(credentialId)
            ?? throw new InvalidOperationException($"Kredensial dengan ID '{credentialId}' tidak ditemukan.");

        var attachment = item.Attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new InvalidOperationException($"Lampiran dengan ID '{attachmentId}' tidak ditemukan pada kredensial.");

        string encFilePath = Path.Combine(_attachmentsDir, attachment.EncryptedFileName);
        if (!File.Exists(encFilePath))
            throw new FileNotFoundException("Berkas terenkripsi lampiran tidak ditemukan di penyimpanan brankas.", encFilePath);

        string? dir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (var inStream = new FileStream(encFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var outStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await CryptoEngine.DecryptStreamAsync(inStream, outStream, password, null, cancellationToken);
        }
    }

    public async Task<string> DecryptAttachmentToTempAsync(
        string credentialId,
        string attachmentId,
        string password,
        CancellationToken cancellationToken = default)
    {
        var item = GetById(credentialId)
            ?? throw new InvalidOperationException($"Kredensial dengan ID '{credentialId}' tidak ditemukan.");

        var attachment = item.Attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new InvalidOperationException($"Lampiran dengan ID '{attachmentId}' tidak ditemukan pada kredensial.");

        string tempFolder = Path.Combine(Path.GetTempPath(), "WhatInsides", "temp_attachments", attachment.Id);
        Directory.CreateDirectory(tempFolder);
        string tempFilePath = Path.Combine(tempFolder, attachment.FileName);

        await ExportAttachmentAsync(credentialId, attachmentId, tempFilePath, password, cancellationToken);
        return tempFilePath;
    }

    public bool DeleteAttachment(string credentialId, string attachmentId)
    {
        var item = GetById(credentialId);
        if (item == null) return false;

        var attachment = item.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment == null) return false;

        string encFilePath = Path.Combine(_attachmentsDir, attachment.EncryptedFileName);
        if (File.Exists(encFilePath))
        {
            try { File.Delete(encFilePath); } catch { }
        }

        item.Attachments.Remove(attachment);
        item.UpdatedAt = DateTime.Now;
        Save();
        return true;
    }

    public int GetCountByCategory(CredentialCategory category)
    {
        if (category == CredentialCategory.All)
            return _items.Count(x => !x.IsDeleted);
        if (category == CredentialCategory.Favorite)
            return _items.Count(x => x.IsFavorite && !x.IsDeleted);
        if (category == CredentialCategory.Trash)
            return _items.Count(x => x.IsDeleted);

        return _items.Count(x => !x.IsDeleted && IsMatchingCategory(x.Category, category));
    }

    public static bool IsMatchingCategory(CredentialCategory itemCat, CredentialCategory targetCat)
    {
        if (itemCat == targetCat) return true;
        return targetCat switch
        {
            CredentialCategory.Login => itemCat == CredentialCategory.Password,
            CredentialCategory.Finance => itemCat is CredentialCategory.PaymentCard or CredentialCategory.BankAccount,
            CredentialCategory.SecureNote => itemCat == CredentialCategory.SoftwareLicense,
            CredentialCategory.DeveloperServer => itemCat is CredentialCategory.SshToken or CredentialCategory.ServerDatabase,
            _ => false
        };
    }

    public IEnumerable<CredentialItem> Filter(CredentialCategory category, string? searchQuery = null, string? tag = null, string? partition = null)
    {
        IEnumerable<CredentialItem> query = _items;

        if (category == CredentialCategory.All)
        {
            query = query.Where(x => !x.IsDeleted);
        }
        else if (category == CredentialCategory.Favorite)
        {
            query = query.Where(x => x.IsFavorite && !x.IsDeleted);
        }
        else if (category == CredentialCategory.Trash)
        {
            query = query.Where(x => x.IsDeleted);
        }
        else
        {
            query = query.Where(x => !x.IsDeleted && IsMatchingCategory(x.Category, category));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(x => x.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(partition) && partition != "ALL")
        {
            query = query.Where(x => x.VaultPartition.StartsWith(partition, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string q = searchQuery.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Title.ToLowerInvariant().Contains(q) ||
                x.Username.ToLowerInvariant().Contains(q) ||
                (x.Url != null && x.Url.ToLowerInvariant().Contains(q)) ||
                (x.Notes != null && x.Notes.ToLowerInvariant().Contains(q)));
        }

        return query.OrderBy(x => x.Title);
    }

    public void Load()
    {
        if (File.Exists(_storagePath))
        {
            try
            {
                string json = File.ReadAllText(_storagePath);
                var loaded = JsonSerializer.Deserialize<List<CredentialItem>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    foreach (var it in loaded)
                    {
                        it.Category = it.Category switch
                        {
                            CredentialCategory.Password => CredentialCategory.Login,
                            CredentialCategory.PaymentCard or CredentialCategory.BankAccount => CredentialCategory.Finance,
                            CredentialCategory.SoftwareLicense => CredentialCategory.SecureNote,
                            CredentialCategory.SshToken or CredentialCategory.ServerDatabase => CredentialCategory.DeveloperServer,
                            _ => it.Category
                        };
                    }
                    _items.Clear();
                    _items.AddRange(loaded);
                    return;
                }
            }
            catch
            {
                // Fallback to default demo items
            }
        }

        SeedDemoItems();
        Save();
    }

    public void ExportJson(string filePath)
    {
        var activeItems = _items.Where(x => !x.IsDeleted).ToList();
        string json = JsonSerializer.Serialize(activeItems, JsonOptions);
        File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
    }

    public void ExportCsv(string filePath)
    {
        var activeItems = _items.Where(x => !x.IsDeleted).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Title,Category,Username,Password,Url,TotpSecret,Notes,VaultPartition,Tags");

        foreach (var it in activeItems)
        {
            string tags = string.Join(";", it.Tags ?? new List<string>());
            sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\"",
                EscapeCsv(it.Title),
                it.Category,
                EscapeCsv(it.Username),
                EscapeCsv(it.Password),
                EscapeCsv(it.Url),
                EscapeCsv(it.TotpSecret),
                EscapeCsv(it.Notes),
                EscapeCsv(it.VaultPartition),
                EscapeCsv(tags)));
        }

        File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
    }

    public (int Count, string Message) ImportJsonOrCsv(string filePath)
    {
        if (!File.Exists(filePath)) return (0, "File tidak ditemukan.");

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".csv")
        {
            return ImportCsv(filePath);
        }
        else
        {
            return ImportJson(filePath);
        }
    }

    private (int Count, string Message) ImportCsv(string filePath)
    {
        int count = 0;
        var lines = File.ReadAllLines(filePath);
        if (lines.Length <= 1) return (0, "File CSV kosong atau hanya memiliki header.");

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var cols = ParseCsvLine(line);
            if (cols.Count == 0) continue;

            string title = cols.Count > 0 ? cols[0] : "Impor Kredensial";
            string categoryStr = cols.Count > 1 ? cols[1] : "Login";
            string username = cols.Count > 2 ? cols[2] : "";
            string password = cols.Count > 3 ? cols[3] : "";
            string url = cols.Count > 4 ? cols[4] : "";
            string totp = cols.Count > 5 ? cols[5] : "";
            string notes = cols.Count > 6 ? cols[6] : "";
            string partition = cols.Count > 7 ? cols[7] : "D:\\ Data & Games";
            string tagsStr = cols.Count > 8 ? cols[8] : "";

            if (!Enum.TryParse<CredentialCategory>(categoryStr, true, out var cat))
            {
                cat = CredentialCategory.Login;
            }

            var tags = tagsStr.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().StartsWith('#') ? t.Trim() : "#" + t.Trim())
                .ToList();

            var item = new CredentialItem
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Kredensial Impor" : title,
                Category = cat,
                Username = username,
                Password = password,
                Url = url,
                TotpSecret = totp,
                Notes = notes,
                VaultPartition = partition,
                Tags = tags,
                UpdatedAt = DateTime.Now
            };

            _items.Insert(0, item);
            count++;
        }

        Save();
        return (count, $"{count} akun berhasil diimpor dari CSV.");
    }

    private (int Count, string Message) ImportJson(string filePath)
    {
        try
        {
            string content = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(content);

            // 1. Bitwarden export format: { "items": [ { "name": "...", "login": { ... } } ] }
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("items", out var bwItems) && bwItems.ValueKind == JsonValueKind.Array)
            {
                int bwCount = 0;
                foreach (var el in bwItems.EnumerateArray())
                {
                    string name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "Bitwarden Item" : "Bitwarden Item";
                    string notes = el.TryGetProperty("notes", out var nt) ? nt.GetString() ?? "" : "";

                    string user = "";
                    string pass = "";
                    string totp = "";
                    string uri = "";

                    if (el.TryGetProperty("login", out var loginEl))
                    {
                        if (loginEl.TryGetProperty("username", out var u)) user = u.GetString() ?? "";
                        if (loginEl.TryGetProperty("password", out var p)) pass = p.GetString() ?? "";
                        if (loginEl.TryGetProperty("totp", out var t)) totp = t.GetString() ?? "";
                        if (loginEl.TryGetProperty("uris", out var urisEl) && urisEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var uObj in urisEl.EnumerateArray())
                            {
                                if (uObj.TryGetProperty("uri", out var uriVal))
                                {
                                    uri = uriVal.GetString() ?? "";
                                    break;
                                }
                            }
                        }
                    }

                    var item = new CredentialItem
                    {
                        Title = name,
                        Category = CredentialCategory.Login,
                        Username = user,
                        Password = pass,
                        TotpSecret = totp,
                        Url = uri,
                        Notes = notes,
                        Tags = new List<string> { "#Bitwarden" },
                        UpdatedAt = DateTime.Now
                    };

                    _items.Insert(0, item);
                    bwCount++;
                }

                Save();
                return (bwCount, $"{bwCount} item berhasil diimpor dari format Bitwarden.");
            }

            // 2. WhatInsides standard list: [ { "Title": "...", "Username": "...", ... } ]
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var directItems = JsonSerializer.Deserialize<List<CredentialItem>>(content);
                if (directItems != null && directItems.Count > 0)
                {
                    foreach (var item in directItems)
                    {
                        item.Id = Guid.NewGuid().ToString();
                        item.UpdatedAt = DateTime.Now;
                        _items.Insert(0, item);
                    }
                    Save();
                    return (directItems.Count, $"{directItems.Count} akun berhasil diimpor.");
                }
            }

            return (0, "Format file JSON tidak dikenali.");
        }
        catch (Exception ex)
        {
            return (0, $"Gagal mengimpor JSON: {ex.Message}");
        }
    }

    private static string EscapeCsv(string? val) => (val ?? string.Empty).Replace("\"", "\"\"");

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var cur = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    cur.Append('\"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(cur.ToString());
                cur.Clear();
            }
            else
            {
                cur.Append(c);
            }
        }
        result.Add(cur.ToString());
        return result;
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_items, JsonOptions);
            File.WriteAllText(_storagePath, json);
        }
        catch
        {
            // Logging or ignored in sandbox
        }
    }

    private void SeedDemoItems()
    {
        _items.Clear();

        _items.Add(new CredentialItem
        {
            Title = "Valorant (Riot Games)",
            Category = CredentialCategory.Login,
            Username = "radiant_hunter#IDN",
            Password = "R10t#G4mer$2025!Valo",
            TotpSecret = "JBSWY3DPEHPK3PXP",
            Url = "https://playvalorant.com",
            VaultPartition = "D:\\ Data & Games",
            Tags = new List<string> { "#Gaming", "#Pribadi" },
            IsFavorite = true,
            Notes = "Akun utama rank Immortal/Radiant. Dilindungi dengan 2FA Riot Mobile Authenticator. Pembayaran linked dengan BCA Visa Signature."
        });

        _items.Add(new CredentialItem
        {
            Title = "AWS Root Console",
            Category = CredentialCategory.DeveloperServer,
            Username = "root_security@enterprise.io",
            Password = "Aws9#Prod*Sec92Cloud!",
            TotpSecret = "HXDMVJECJJWSRB3HWIZR4IFUGFTMXBOZ",
            Url = "https://aws.amazon.com",
            VaultPartition = "D:\\ Data & Games",
            Tags = new List<string> { "#Kantor" },
            IsFavorite = true,
            Notes = "Root MFA terikat pada YubiKey fisik dan Authenticator backup."
        });

        _items.Add(new CredentialItem
        {
            Title = "BCA Visa Signature",
            Category = CredentialCategory.Finance,
            Username = "•••• •••• •••• 8842",
            Password = "Exp 08/28 | CVV 719",
            Url = "https://klikbca.com",
            VaultPartition = "D:\\ Data & Games",
            Tags = new List<string> { "#Keuangan" },
            IsFavorite = true,
            Notes = "Limit harian Rp 50.000.000 untuk transaksi gaming dan cloud server."
        });

        _items.Add(new CredentialItem
        {
            Title = "Epic Games Store",
            Category = CredentialCategory.Login,
            Username = "nexus_warrior",
            Password = "Ep!c$G4m3s2026Secure",
            Url = "https://epicgames.com",
            VaultPartition = "D:\\ Data & Games",
            Tags = new List<string> { "#Gaming" },
            IsFavorite = false,
            Notes = "Koleksi library game mingguan gratis."
        });

        _items.Add(new CredentialItem
        {
            Title = "Ethereum Ledger Seed",
            Category = CredentialCategory.CryptoSeed,
            Username = "0x91F...E940",
            Password = "dipper-pulsar-proven-english-fay-cosmic-falcon-orbit-horizon-anchor-velvet-cipher",
            VaultPartition = "E:\\ Koleksi",
            Tags = new List<string> { "#Keuangan", "#Pribadi" },
            IsFavorite = true,
            Notes = "BIP-39 Mnemonic 24 kata tersimpan dalam kontainer terenkripsi AES-256 in-place."
        });

        _items.Add(new CredentialItem
        {
            Title = "GitHub",
            Category = CredentialCategory.DeveloperServer,
            Username = "octo_developer",
            Password = "ghp_K91zXv7A938FldkqM29Vv81273kdksA",
            TotpSecret = "KRUGS4ZANFZSAYJA",
            Url = "https://github.com",
            VaultPartition = "D:\\ Data & Games",
            Tags = new List<string> { "#Kantor" },
            IsFavorite = false,
            Notes = "Personal Access Token untuk deployment automated CI/CD pipeline."
        });

        _items.Add(new CredentialItem
        {
            Title = "Steam Vault",
            Category = CredentialCategory.Login,
            Username = "cyber_spectre",
            Password = "St34m!P0w3r#2026V4ult",
            TotpSecret = "GEZDGNBVGY3TQOJQ",
            Url = "https://steampowered.com",
            VaultPartition = "D:\\ Data & Games",
            Tags = new List<string> { "#Gaming" },
            IsFavorite = false,
            Notes = "Steam Guard Mobile terhubung."
        });
    }
}
