﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Diagnostics;
using System.Threading;
using TomanuExtensions.Utils;
using NHibernate.Mapping.ByCode;

namespace MangaCrawlerLib
{
    public class Page : IClassMapping
    {
        public virtual int ID { get; protected set; }
        public virtual Chapter Chapter { get; protected set; }
        protected virtual int Version { get; set; }
        public virtual int Index { get; protected set; }
        public virtual string URL { get; protected set; }
        public virtual byte[] Hash { get; protected set; }
        public virtual string ImageURL { get; protected set; }
        public virtual bool Downloaded { get; protected set; }
        public virtual string Name { get; protected set; }
        public virtual string ImageFilePath { get; protected set; }

        protected Page()
        {
        }

        internal Page(Chapter a_chapter, string a_url, int a_index, string a_name = null)
        {
            Chapter = a_chapter;
            URL = HttpUtility.HtmlDecode(a_url);
            Index = a_index;

            if (a_name != null)
            {
                a_name = a_name.Trim();
                a_name = a_name.Replace("\t", " ");
                while (a_name.IndexOf("  ") != -1)
                    a_name = a_name.Replace("  ", " ");
                a_name = HttpUtility.HtmlDecode(a_name);
                Name = FileUtils.RemoveInvalidFileDirectoryCharacters(a_name);
            }
            else
                Name = Index.ToString();
        }

        private void Map(ModelMapper a_mapper)
        {
            a_mapper.Class<Page>(m =>
            {
                m.Id(c => c.ID, mapper => mapper.Generator(Generators.Native));
                m.Version("Version", mapper => { });
                m.Property(c => c.Index, mapper => { });
                m.Property(c => c.URL, mapping => mapping.NotNullable(true));
                m.Property(c => c.Downloaded, mapping => { });
                m.Property(c => c.ImageFilePath, mapping => mapping.NotNullable(false));
                m.Property(c => c.Name, mapping => mapping.NotNullable(true));
                m.Property(c => c.ImageURL, mapping => mapping.NotNullable(false));
                m.Property(c => c.Hash, mapping => mapping.NotNullable(false));
                m.ManyToOne(
                    c => c.Chapter,
                    mapping =>
                    {
                        mapping.Fetch(FetchKind.Join);
                        mapping.NotNullable(false);
                        //mapping.Insert(false); 
                        //mapping.Update(false);
                    }
                );
            });
        }

        protected internal virtual CustomTaskScheduler Scheduler
        {
            get
            {
                return Chapter.Scheduler;
            }
        }

        protected internal virtual Crawler Crawler
        {
            get
            {
                return Chapter.Crawler;
            }
        }

        public virtual Server Server
        {
            get
            {
                return Chapter.Server;
            }
        }

        public virtual Serie Serie
        {
            get
            {
                return Chapter.Serie;
            }
        }

        public override string ToString()
        {
            return String.Format("{0} - {1}/{2}",
                    Chapter, Index, Chapter.PagesCount);
        }

        protected internal virtual MemoryStream GetImageStream()
        {
            if (ImageURL == null)
            {
                ImageURL = HttpUtility.HtmlDecode(Crawler.GetImageURL(this));
                NH.TransactionLockUpdate(this, () => { });
            }

            return Crawler.GetImageStream(this);  
        }

        protected internal virtual void DownloadAndSavePageImage()
        {
            new DirectoryInfo(Chapter.ChapterDir).Create();

            FileInfo temp_file = new FileInfo(Path.GetTempFileName());

            try
            {
                using (FileStream file_stream = new FileStream(temp_file.FullName, FileMode.Create))
                {
                    MemoryStream ms = null;

                    try
                    {
                        ms = GetImageStream();
                    }
                    catch (WebException ex)
                    {
                        // Some images are unavailable, if we get null pernamently tests
                        // will detect this.
                        Loggers.MangaCrawler.Fatal("Exception #1", ex);
                        return;
                    }

                    try
                    {
                        System.Drawing.Image.FromStream(ms);
                        ms.Position = 0;
                    }
                    catch (Exception ex)
                    {
                        // Some junks.
                        Loggers.MangaCrawler.Fatal("Exception #2", ex);
                        return;
                    }

                    ms.CopyTo(file_stream);

                    ms.Position = 0;
                    byte[] hash;
                    TomanuExtensions.Utils.Hash.CalculateSHA256(ms, out hash);
                    NH.TransactionLockUpdate(this, () => Hash = hash);
                }

                NH.TransactionLockUpdate(this, () =>
                {
                    ImageFilePath = Chapter.ChapterDir +
                        FileUtils.RemoveInvalidFileDirectoryCharacters(Name) +
                        FileUtils.RemoveInvalidFileDirectoryCharacters(
                            Path.GetExtension(ImageURL).ToLower());
                });

                FileInfo image_file = new FileInfo(ImageFilePath);

                if (image_file.Exists)
                    image_file.Delete();

                temp_file.MoveTo(image_file.FullName);

                NH.TransactionLockUpdate(this, () => Downloaded = true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Loggers.MangaCrawler.Fatal("Exception #3", ex);
                NH.TransactionLockUpdate(this, () =>
                {
                    Downloaded = false;
                    Hash = null;
                    ImageFilePath = null;
                    ImageURL = null;
                });
                throw;
            }
            finally
            {
                if (temp_file.Exists)
                    temp_file.Delete();
            }
        }

        protected internal virtual bool ImageExists()
        {
            // TODO: na podstawie sciezki pliku i jego hasu dokonac weryfikacji
            throw new NotImplementedException();
        }
    }
}
