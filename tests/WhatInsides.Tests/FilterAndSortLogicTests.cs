using System;
using System.Collections.Generic;
using System.Linq;
using WhatInsides.Models;
using Xunit;

namespace WhatInsides.Tests;

public class FilterAndSortLogicTests
{
    private List<LockedItemModel> GetSampleItems()
    {
        return new List<LockedItemModel>
        {
            new LockedItemModel
            {
                Name = "Alpha.pdf",
                OriginalPath = @"D:\Alpha.pdf",
                CurrentPath = @"D:\Alpha.pdf.wisafe",
                LockType = "Aes256",
                FileSize = 500,
                DateLocked = new DateTime(2026, 1, 1)
            },
            new LockedItemModel
            {
                Name = "Zeta.docx",
                OriginalPath = @"C:\Zeta.docx",
                CurrentPath = @"C:\Zeta.docx.wisafe",
                LockType = "Aes256",
                FileSize = 1000,
                DateLocked = new DateTime(2026, 5, 1)
            },
            new LockedItemModel
            {
                Name = "BetaFolder",
                OriginalPath = @"D:\BetaFolder",
                CurrentPath = @"D:\BetaFolder.{wi_fast_locked}",
                LockType = "FastHide",
                FileSize = 200,
                DateLocked = new DateTime(2026, 3, 1)
            }
        };
    }

    [Fact]
    public void PartitionFilter_CorrectlyFiltersDAndC()
    {
        var items = GetSampleItems();

        var driveD = items.Where(it => (it.OriginalPath != null && it.OriginalPath.StartsWith("D:", StringComparison.OrdinalIgnoreCase)) ||
                                       (it.CurrentPath != null && it.CurrentPath.StartsWith("D:", StringComparison.OrdinalIgnoreCase))).ToList();

        var driveC = items.Where(it => (it.OriginalPath != null && it.OriginalPath.StartsWith("C:", StringComparison.OrdinalIgnoreCase)) ||
                                       (it.CurrentPath != null && it.CurrentPath.StartsWith("C:", StringComparison.OrdinalIgnoreCase))).ToList();

        Assert.Equal(2, driveD.Count);
        Assert.Single(driveC);
        Assert.Equal("Zeta.docx", driveC[0].Name);
    }

    [Fact]
    public void SearchQuery_MatchesNamePathAndType()
    {
        var items = GetSampleItems();

        // Search by Name
        var result1 = items.Where(it => it.Name.Contains("Alpha", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Single(result1);

        // Search by Type
        var result2 = items.Where(it => it.LockType.Contains("FastHide", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Single(result2);
        Assert.Equal("BetaFolder", result2[0].Name);
    }

    [Fact]
    public void Sorting_CorrectlyOrdersItems()
    {
        var items = GetSampleItems();

        // Name Ascending
        var sortedNameAsc = items.OrderBy(x => x.Name).ToList();
        Assert.Equal("Alpha.pdf", sortedNameAsc[0].Name);
        Assert.Equal("BetaFolder", sortedNameAsc[1].Name);
        Assert.Equal("Zeta.docx", sortedNameAsc[2].Name);

        // Name Descending
        var sortedNameDesc = items.OrderByDescending(x => x.Name).ToList();
        Assert.Equal("Zeta.docx", sortedNameDesc[0].Name);

        // Date Ascending
        var sortedDateAsc = items.OrderBy(x => x.DateLocked).ToList();
        Assert.Equal("Alpha.pdf", sortedDateAsc[0].Name); // Jan 2026

        // Size Descending
        var sortedSizeDesc = items.OrderByDescending(x => x.FileSize).ToList();
        Assert.Equal("Zeta.docx", sortedSizeDesc[0].Name); // 1000 bytes

        // Size Ascending
        var sortedSizeAsc = items.OrderBy(x => x.FileSize).ToList();
        Assert.Equal("BetaFolder", sortedSizeAsc[0].Name); // 200 bytes

        // Type Ascending by extension then name
        var sortedType = items.OrderBy(x => x.IsFolder ? "" : System.IO.Path.GetExtension(x.Name ?? ""), StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal("BetaFolder", sortedType[0].Name); // empty extension first
        Assert.Equal("Zeta.docx", sortedType[1].Name);  // .docx
        Assert.Equal("Alpha.pdf", sortedType[2].Name);   // .pdf
    }

    [Theory]
    [InlineData("pdf alpha", 1, "Alpha.pdf")]
    [InlineData("zeta docx", 1, "Zeta.docx")]
    [InlineData("ALPHA", 1, "Alpha.pdf")]
    [InlineData("d:\\alpha", 1, "Alpha.pdf")]
    [InlineData("Document", 2, null)] // Alpha.pdf and Zeta.docx are both Documents
    [InlineData("Folder", 1, "BetaFolder")]
    [InlineData("NonExistentXYZ999", 0, null)]
    [InlineData("", 3, null)]
    public void MultiTokenFastSearch_FiltersAccurately(string query, int expectedCount, string? expectedFirstItemName)
    {
        var items = GetSampleItems();
        string search = query.Trim();

        IEnumerable<LockedItemModel> filtered = items;
        if (!string.IsNullOrEmpty(search))
        {
            var tokens = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            filtered = filtered.Where(it =>
            {
                string name = it.Name ?? string.Empty;
                string path = it.OriginalPath ?? string.Empty;
                string type = it.LockType ?? string.Empty;
                string ext = System.IO.Path.GetExtension(name);
                string cat = LockedItemModel.DetectCategory(path, it.IsFolder);

                foreach (var token in tokens)
                {
                    bool match = name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                 path.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                 type.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                 ext.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains(token, StringComparison.OrdinalIgnoreCase);
                    if (!match) return false;
                }
                return true;
            });
        }

        var results = filtered.ToList();
        Assert.Equal(expectedCount, results.Count);
        if (expectedFirstItemName != null && results.Count > 0)
        {
            Assert.Equal(expectedFirstItemName, results[0].Name);
        }
    }
}
