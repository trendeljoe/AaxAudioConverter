﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace audiamus.aaxconv.lib {
  class AudibleAppContentMetadata {

    struct ContentMetadataFile {
      public readonly string Filename;
      public readonly string ASIN;

      public ContentMetadataFile (string filename, string asin) {
        Filename = filename;
        ASIN = asin;
      }
    }

    public const string CONTENT = "Content";
    const string FILESCACHE = "filescache";
    const string CONTENT_METADATA = "content_metadata_";
    const string JSON = ".json";

    const string REGEX = CONTENT_METADATA + @"(\w+)"; 
    static readonly Regex __regexAsin = new Regex (REGEX, RegexOptions.Compiled); 

    public void GetContentMetadata (Book.BookPart part) {

      string contentMetadataFile = findContentMetadataFile (part.AaxFileItem.FileName);
      if (contentMetadataFile is null)
        return;

      string json = File.ReadAllText (contentMetadataFile);
      var metadata = JsonConvert.DeserializeObject<json.AppContentMetadata> (json);

      // set the chapters
      var chapters = new List<Chapter> ();
      var metaChapters = metadata.content_metadata.chapter_info.chapters;
      if (metaChapters.Count == 0)
        return;
      bool hasBlanks = metaChapters.Where (c => string.IsNullOrWhiteSpace (c.title) || c.length_ms == 0).Any ();
      if (hasBlanks)
        return;

      foreach (var ch in metaChapters) {
        var chapter = new Chapter ();
        chapter.Name = ch.title.Trim();
        chapter.Time.Begin = TimeSpan.FromMilliseconds (ch.start_offset_ms);
        chapter.Time.End = TimeSpan.FromMilliseconds (ch.start_offset_ms + ch.length_ms);
        chapters.Add (chapter);
      }
      part.Chapters2 = chapters;

      // set brandintro and brandoutro times
      part.BrandIntro = TimeSpan.FromMilliseconds (metadata.content_metadata.chapter_info.brandIntroDurationMs);
      part.BrandOutro = TimeSpan.FromMilliseconds (metadata.content_metadata.chapter_info.brandOutroDurationMs);
      part.Duration = TimeSpan.FromMilliseconds (metadata.content_metadata.chapter_info.runtime_length_ms);
    }

    private string findContentMetadataFile (string fileName) {
      // try to find content metadata files, either relative or absolute

      // relative
      string cntDir = Path.GetDirectoryName (fileName);
      var diLocalState = Directory.GetParent (cntDir);
      string localStateDir = diLocalState.FullName;
      string cacheDir = Path.Combine (localStateDir, FILESCACHE);
      var dirs = new List<string> ();
      if (Directory.Exists (cacheDir))
        dirs.Add (cacheDir);
      else {
        // absolute
        var absDirs = ActivationCodeApp.GetPackageDirectories ();
        if (absDirs is null)
          return null;
        foreach (var absDir in absDirs) {
          cacheDir = Path.Combine (absDir, FILESCACHE);
          if (Directory.Exists (cacheDir))
            dirs.Add (cacheDir);
        }
      }

      if (dirs.Count == 0)
        return null;

      // get all content metadata filenames
      var files = dirs.SelectMany (d => Directory.GetFiles (d, $"{CONTENT_METADATA}*{JSON}")).Distinct ();

      // isolate asin
      var asinFiles = new List<ContentMetadataFile> ();
      foreach (string file in files) {
        var fn = Path.GetFileNameWithoutExtension (file);
        var match = __regexAsin.Match (fn);
        if (!match.Success)
          continue;
        string asin = match.Groups[1].Value;
        asinFiles.Add (new ContentMetadataFile (file, asin));
      }

      // find matching asin in our filename
      var filnam = Path.GetFileNameWithoutExtension (fileName);
      string contentMetafile = asinFiles.Where (k => filnam.Contains (k.ASIN)).Select (k => k.Filename).FirstOrDefault ();

      return contentMetafile;
    }
  }
}