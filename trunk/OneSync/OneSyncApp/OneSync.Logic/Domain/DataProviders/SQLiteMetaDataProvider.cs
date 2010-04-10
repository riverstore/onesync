﻿/*
 $Id: SQLiteMetaDataProvider.cs 255 2010-03-17 16:08:53Z gclin009 $
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Community.CsharpSqlite.SQLiteClient;
using Community.CsharpSqlite;
using System.IO;
using System.Data;


namespace OneSync.Synchronization
{
    // TODO: wrap all db stmts in try catch and return false if failed?

    public class SQLiteMetaDataProvider : MetaDataProvider
    {

        /// <summary>
        /// Creates an SQLiteMetaDataProvider that manages metadata stored in specified path.
        /// </summary>
        /// <param name="storagePath">Location where all metadata are saved to or loaded from.</param>
        /// <param name="rootPath">To be combined with relative paths to give absolute paths of files.</param>
        public SQLiteMetaDataProvider(string storagePath, string rootPath)
            : base(storagePath, rootPath)
        {
            // Create database schema if necessary
            FileInfo fi = new FileInfo(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME));
            if (!fi.Exists) { fi.Directory.Create(); }
        }

        #region Load Metadata
        /// <summary>
        /// Load Metadata from database
        /// </summary>
        /// <param name="currId"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public override Metadata Load(string currId, SourceOption option)
        {
            return new Metadata(LoadFileMetadata(currId, option), LoadFolderMetadata(currId, option));
        }

        public override FileMetaData LoadFileMetadata(string currId, SourceOption option)
        {
            string opt = (option == SourceOption.SOURCE_ID_NOT_EQUALS) ? " <> " : " = ";
            FileMetaData mData = new FileMetaData(currId, this.RootPath);

            SQLiteAccess db = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);
            using (SqliteConnection con = db.NewSQLiteConnection())
            {
                string cmdText = "SELECT * FROM " + Configuration.TBL_METADATA +
                                 " WHERE " + Configuration.COL_SOURCE_ID + opt + " @sourceId";

                SqliteParameterCollection paramList = new SqliteParameterCollection();
                paramList.Add(new SqliteParameter("@sourceId", DbType.String) { Value = currId });

                db.ExecuteReader(cmdText, paramList, reader =>
                {
                    mData.MetaDataItems.Add(new FileMetaDataItem(
                        (string)reader[Configuration.COL_SOURCE_ID],
                        this.RootPath + (string)reader[Configuration.COL_RELATIVE_PATH], (string)reader[Configuration.COL_RELATIVE_PATH],
                        (string)reader[Configuration.COL_HASH_CODE], (DateTime)reader[Configuration.COL_LAST_MODIFIED_TIME],
                        Convert.ToUInt32(reader[Configuration.COL_NTFS_ID1]), Convert.ToUInt32(reader[Configuration.COL_NTFS_ID2])));
                }
                );
            }
            return mData;
        }

        public override FolderMetadata LoadFolderMetadata(string currId, SourceOption option)
        {
            string opt = (option == SourceOption.SOURCE_ID_NOT_EQUALS) ? " <> " : " = ";
            FolderMetadata folderMetadata = new FolderMetadata(currId, this.RootPath);

            SQLiteAccess db = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);
            using (SqliteConnection con = db.NewSQLiteConnection())
            {
                string cmdText = "SELECT * FROM " + Configuration.TLB_FOLDERMETADATA +
                    " WHERE " + Configuration.COL_SOURCE_ID + opt + " @sourceId";
                SqliteParameterCollection paramList = new SqliteParameterCollection();
                paramList.Add(new SqliteParameter("@sourceId", DbType.String) { Value = currId });
                db.ExecuteReader(cmdText, paramList, reader =>
                {
                    folderMetadata.FolderMetadataItems.Add(
                        new FolderMetadataItem(
                            (string)reader[Configuration.COL_SOURCE_ID],
                            (string)reader[Configuration.COL_FOLDER_RELATIVE_PATH]));
                }
                );
            }
            return folderMetadata;
        }






        #endregion Load Metadata

        #region Add Metadata
        public override bool Add(FileMetaData mData)
        {
            return this.Add(mData.MetaDataItems);
        }

        /// <summary>
        /// Add Folder metadata into datatabse
        /// Add is atomic action
        /// </summary>
        /// <param name="folders"></param>
        /// <returns></returns>
        public bool Add(IList<FolderMetadataItem> folders)
        {
            SQLiteAccess db = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);
            using (SqliteConnection con = db.NewSQLiteConnection())
            {
                SqliteTransaction trasaction = (SqliteTransaction)con.BeginTransaction();
                try
                {
                    foreach (FolderMetadataItem item in folders)
                    {
                        string cmdText = "INSERT INTO " + Configuration.TLB_FOLDERMETADATA +
                                     "( " + Configuration.COL_SOURCE_ID + "," + Configuration.COL_FOLDER_RELATIVE_PATH + ")" +
                                     "VALUES (@source_id , @relative_path)";

                        SqliteParameterCollection paramList = new SqliteParameterCollection();
                        paramList.Add(new SqliteParameter("@source_id", DbType.String) { Value = item.SourceId });
                        paramList.Add(new SqliteParameter("@relative_path", DbType.String) { Value = item.RelativePath });
                        db.ExecuteNonQuery(cmdText, paramList);
                    }
                    trasaction.Commit();
                    return true;
                }
                catch (Exception)
                {
                    trasaction.Rollback();
                    return false;
                }
            }
        }

        /// <summary>
        /// Add a list of folder metadata items into database
        /// Combinational transaction feature is supported
        /// </summary>
        /// <param name="folders"></param>
        /// <param name="con"></param>
        /// <returns></returns>
        public bool Add(IList<FolderMetadataItem> folders, SqliteConnection con)
        {
            try
            {
                foreach (FolderMetadataItem item in folders)
                {
                    string cmdText = "INSERT INTO " + Configuration.TLB_FOLDERMETADATA +
                                     "( " + Configuration.COL_SOURCE_ID + "," + Configuration.COL_FOLDER_RELATIVE_PATH + ")" +
                                     "VALUES (@source_id , @relative_path)";
                    using (SqliteCommand cmd = new SqliteCommand(cmdText, con))
                    {
                        cmd.Parameters.Add(new SqliteParameter("@source_id", DbType.String) { Value = item.SourceId });
                        cmd.Parameters.Add(new SqliteParameter("@relative_path", DbType.String) { Value = item.RelativePath });
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            { return false; }
            return true;
        }
        /// <summary>
        /// Add a list of file metadata item into database
        /// Actom action 
        /// </summary>
        /// <param name="mData"></param>
        /// <returns></returns>
        public override bool Add(IList<FileMetaDataItem> mData)
        {
            SQLiteAccess db = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);
            using (SqliteConnection con = db.NewSQLiteConnection())
            {
                SqliteTransaction trasaction = (SqliteTransaction)con.BeginTransaction();
                try
                {
                    foreach (FileMetaDataItem item in mData)
                    {
                        string cmdText = "INSERT INTO " + Configuration.TBL_METADATA +
                                     "( " + Configuration.COL_SOURCE_ID + "," + Configuration.COL_RELATIVE_PATH + "," +
                                     Configuration.COL_HASH_CODE + "," +
                                     Configuration.COL_LAST_MODIFIED_TIME + "," +
                                     Configuration.COL_NTFS_ID1 + "," +
                                     Configuration.COL_NTFS_ID2 + ")" +
                                     "VALUES (@source_id , @relative_path, @hash_code, @last_modified_time, @ntfs_id1, @ntfs_id2) ";

                        SqliteParameterCollection paramList = new SqliteParameterCollection();
                        paramList.Add(new SqliteParameter("@source_id", DbType.String) { Value = item.SourceId });
                        paramList.Add(new SqliteParameter("@relative_path", DbType.String) { Value = item.RelativePath });
                        paramList.Add(new SqliteParameter("@hash_code", DbType.String) { Value = item.HashCode });
                        paramList.Add(new SqliteParameter("@last_modified_time", DbType.DateTime) { Value = item.LastModifiedTime });
                        paramList.Add(new SqliteParameter("@ntfs_id1", DbType.Int32) { Value = item.NTFS_ID1 });
                        paramList.Add(new SqliteParameter("@ntfs_id2", DbType.Int32) { Value = item.NTFS_ID2 });

                        db.ExecuteNonQuery(cmdText, paramList);
                    }
                    trasaction.Commit();
                }
                catch (Exception)
                {
                    trasaction.Rollback();
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Add a list of file metadata into database
        /// Combinational transaction feature is supported
        /// </summary>
        /// <param name="mData"></param>
        /// <param name="con"></param>
        /// <returns></returns>
        public bool Add(IList<FileMetaDataItem> mData, SqliteConnection con)
        {
            try
            {
                foreach (FileMetaDataItem item in mData)
                {
                    string cmdText = "INSERT INTO " + Configuration.TBL_METADATA +
                                     "( " + Configuration.COL_SOURCE_ID + "," + Configuration.COL_RELATIVE_PATH + "," +
                                     Configuration.COL_HASH_CODE + "," +
                                     Configuration.COL_LAST_MODIFIED_TIME + "," +
                                     Configuration.COL_NTFS_ID1 + "," +
                                     Configuration.COL_NTFS_ID2 + ")" +
                                     "VALUES (@source_id , @relative_path, @hash_code, @last_modified_time, @ntfs_id1, @ntfs_id2) ";
                    using (SqliteCommand cmd = new SqliteCommand(cmdText, con))
                    {
                        cmd.Parameters.Add(new SqliteParameter("@source_id", DbType.String) { Value = item.SourceId });
                        cmd.Parameters.Add(new SqliteParameter("@relative_path", DbType.String) { Value = item.RelativePath });
                        cmd.Parameters.Add(new SqliteParameter("@hash_code", DbType.String) { Value = item.HashCode });
                        cmd.Parameters.Add(new SqliteParameter("@last_modified_time", DbType.DateTime) { Value = item.LastModifiedTime });
                        cmd.Parameters.Add(new SqliteParameter("@ntfs_id1", DbType.Int32) { Value = item.NTFS_ID1 });
                        cmd.Parameters.Add(new SqliteParameter("@ntfs_id2", DbType.Int32) { Value = item.NTFS_ID2 });
                        cmd.ExecuteNonQuery();
                    }
                }

            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
        #endregion Add Metadata

        #region Delete metadata
        /// <summary>
        /// Delete a list of file metadata items
        /// Actomic
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public override bool Delete(IList<FileMetaDataItem> items)
        {
            // All deletions are atomic

            SQLiteAccess dbAccess = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);

            using (SqliteConnection con = dbAccess.NewSQLiteConnection())
            {
                SqliteTransaction transaction = (SqliteTransaction)con.BeginTransaction();
                try
                {
                    foreach (FileMetaDataItem item in items)
                    {
                        string cmdText = "DELETE FROM " + Configuration.TBL_METADATA +
                                          " WHERE " + Configuration.COL_SOURCE_ID + " = @sourceId AND " +
                                          Configuration.COL_RELATIVE_PATH + " = @path";

                        SqliteParameterCollection paramList = new SqliteParameterCollection();
                        paramList.Add(new SqliteParameter("@sourceId", DbType.String) { Value = item.SourceId });
                        paramList.Add(new SqliteParameter("@path", DbType.String) { Value = item.RelativePath });
                        dbAccess.ExecuteNonQuery(cmdText, paramList);
                    }
                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    // Log error??
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Delete a list of file metadata items
        /// </summary>
        /// <param name="items"></param>
        /// <param name="con"></param>
        /// <returns></returns>
        public bool Delete(IList<FileMetaDataItem> items, SqliteConnection con)
        {
            try
            {
                foreach (FileMetaDataItem item in items)
                {
                    string cmdText = "DELETE FROM " + Configuration.TBL_METADATA +
                                          " WHERE " + Configuration.COL_SOURCE_ID + " = @sourceId AND " +
                                          Configuration.COL_RELATIVE_PATH + " = @path";
                    using (SqliteCommand cmd = new SqliteCommand(cmdText, con))
                    {
                        cmd.Parameters.Add(new SqliteParameter("@sourceId", DbType.String) { Value = item.SourceId });
                        cmd.Parameters.Add(new SqliteParameter("@path", DbType.String) { Value = item.RelativePath });
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            { return false; }
            return true;
        }

        public bool Delete(IList<FolderMetadataItem> items)
        {
            // All deletions are atomic

            SQLiteAccess dbAccess = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);

            using (SqliteConnection con = dbAccess.NewSQLiteConnection())
            {
                SqliteTransaction transaction = (SqliteTransaction)con.BeginTransaction();
                try
                {
                    foreach (FolderMetadataItem item in items)
                    {
                        string cmdText = "DELETE FROM " + Configuration.TLB_FOLDERMETADATA +
                                          " WHERE " + Configuration.COL_SOURCE_ID + " = @sourceId AND " +
                                          Configuration.COL_RELATIVE_PATH + " = @path";

                        SqliteParameterCollection paramList = new SqliteParameterCollection();
                        paramList.Add(new SqliteParameter("@sourceId", DbType.String) { Value = item.SourceId });
                        paramList.Add(new SqliteParameter("@path", DbType.String) { Value = item.RelativePath });
                        dbAccess.ExecuteNonQuery(cmdText, paramList);
                    }
                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    // Log error??
                    return false;
                }
            }

            return true;
        }

        public bool Delete(IList<FolderMetadataItem> items, SqliteConnection con)
        {
            try
            {
                foreach (FolderMetadataItem item in items)
                {
                    string cmdText = "DELETE FROM " + Configuration.TLB_FOLDERMETADATA +
                                          " WHERE " + Configuration.COL_SOURCE_ID + " = @sourceId AND " +
                                          Configuration.COL_RELATIVE_PATH + " = @path";

                    using (SqliteCommand cmd = new SqliteCommand(cmdText, con))
                    {
                        cmd.Parameters.Add(new SqliteParameter("@sourceId", DbType.String) { Value = item.SourceId });
                        cmd.Parameters.Add(new SqliteParameter("@path", DbType.String) { Value = item.RelativePath });
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            { return false; }
            return true;
        }

        #endregion Delete metadata





        #region Update Metadata
        /// <summary>
        /// Update meta data including file metadata and folder metadata
        /// </summary>
        /// <param name="oldMetadata"></param>
        /// <param name="newMetada"></param>
        /// <returns></returns>
        public override bool Update(Metadata oldMetadata, Metadata newMetada)
        {
            SQLiteAccess db = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);
            using (SqliteConnection con = db.NewSQLiteConnection())
            {
                SqliteTransaction trasaction = (SqliteTransaction)con.BeginTransaction();
                // TODO: make 3 actions atomic
                try
                {
                    if (!UpdateFileMetadata(oldMetadata.FileMetadata, newMetada.FileMetadata, con))
                        throw new Exception();
                    if (!UpdateFolderMetadata(oldMetadata.FolderMetadata, newMetada.FolderMetadata, con))
                        throw new Exception();
                    trasaction.Commit();
                }
                catch (Exception)
                {
                    trasaction.Rollback();
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Update folder metadata
        /// </summary>
        /// <param name="oldItems"></param>
        /// <param name="newItems"></param>
        /// <returns></returns>
        private bool UpdateFolderMetadata(FolderMetadata oldMetadata, FolderMetadata newMetadata, SqliteConnection con)
        {
            //Find the metadata items that in the current folder only
            IEnumerable<FolderMetadataItem> newOnly = from _new in newMetadata.FolderMetadataItems
                                                      where !oldMetadata.FolderMetadataItems.Contains(_new, new FolderMetadataItemComparer())
                                                      select _new;

            //find metadata items that not in the current folder but metadata store
            IEnumerable<FolderMetadataItem> oldOnly = from _old in oldMetadata.FolderMetadataItems
                                                      where !newMetadata.FolderMetadataItems.Contains(_old, new FolderMetadataItemComparer())
                                                      select _old;
            try
            {
                this.Add(newOnly.ToList(), con);
                this.Delete(oldOnly.ToList(), con);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public override bool UpdateFolderMetadata(FolderMetadata oldMetadata, FolderMetadata newMetadata)
        {
            oldMetadata.FolderMetadataItems.ToList().Sort(new FolderMetadataItemComparer());
            newMetadata.FolderMetadataItems.ToList().Sort(new FolderMetadataItemComparer());

            //Find the metadata items that in the current folder only
            IEnumerable<FolderMetadataItem> newOnly = from _new in newMetadata.FolderMetadataItems
                                                      where !oldMetadata.FolderMetadataItems.Contains(_new, new FolderMetadataItemComparer())
                                                      select _new;

            //find metadata items that not in the current folder but metadata store
            IEnumerable<FolderMetadataItem> oldOnly = from _old in oldMetadata.FolderMetadataItems
                                                      where !newMetadata.FolderMetadataItems.Contains(_old, new FolderMetadataItemComparer())
                                                      select _old;

            SQLiteAccess db = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);
            using (SqliteConnection con = db.NewSQLiteConnection())
            {
                SqliteTransaction transaction = (SqliteTransaction)con.BeginTransaction();
                try
                {
                    this.Add(newOnly.ToList(), con);
                    this.Delete(oldOnly.ToList(), con);
                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    return false;
                }
            }

            return true;
        }


        public bool Update(IList<FileMetaDataItem> items, SqliteConnection con)
        {
            try
            {
                foreach (FileMetaDataItem item in items)
                {
                    string cmdText = "UPDATE " + Configuration.TBL_METADATA +
                                         " SET " + Configuration.COL_HASH_CODE + " = @hash, " +
                                         Configuration.COL_LAST_MODIFIED_TIME + " = @lmf" +
                                         " WHERE " + Configuration.COL_RELATIVE_PATH + " = @rel AND " +
                                         Configuration.COL_SOURCE_ID + " = @sourceId";
                    using (SqliteCommand cmd = new SqliteCommand(cmdText, con))
                    {
                        cmd.Parameters.Add(new SqliteParameter("@hash", DbType.String) { Value = item.HashCode });
                        cmd.Parameters.Add(new SqliteParameter("@lmf", DbType.DateTime) { Value = item.LastModifiedTime });
                        cmd.Parameters.Add(new SqliteParameter("@rel", DbType.String) { Value = item.RelativePath });
                        cmd.Parameters.Add(new SqliteParameter("@sourceId", DbType.String) { Value = item.SourceId });
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private bool UpdateFileMetadata(FileMetaData oldMetadata, FileMetaData newMetadata, SqliteConnection con)
        {
            //Sort list of metadata items before search using linq
            oldMetadata.MetaDataItems.ToList().Sort(new FileMetaDataItemComparer());
            newMetadata.MetaDataItems.ToList().Sort(new FileMetaDataItemComparer());

            //Get newly created items by comparing relative paths
            //newOnly is metadata item in current metadata but not in old one
            IEnumerable<FileMetaDataItem> newOnly = from _new in newMetadata.MetaDataItems
                                                    where !oldMetadata.MetaDataItems.Contains(_new, new FileMetaDataItemComparer())
                                                    select _new;

            //Get deleted items           
            IEnumerable<FileMetaDataItem> oldOnly = from old in oldMetadata.MetaDataItems
                                                    where !newMetadata.MetaDataItems.Contains(old, new FileMetaDataItemComparer())
                                                    select old;

            //get the items from 2 metadata with same relative paths but different hashes.
            IEnumerable<FileMetaDataItem> bothModified = from _new in newMetadata.MetaDataItems
                                                         from old in oldMetadata.MetaDataItems
                                                         where _new.RelativePath.Equals((old).RelativePath)
                                                         && !_new.HashCode.Equals(old.HashCode)
                                                         select _new;

            try
            {
                this.Add(newOnly.ToList(), con);
                this.Delete(oldOnly.ToList(), con);
                this.Update(bothModified.ToList(), con);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public override bool UpdateFileMetadata(FileMetaData oldMetadata, FileMetaData newMetadata)
        {
            //Sort list of metadata items before search using linq
            oldMetadata.MetaDataItems.ToList().Sort(new FileMetaDataItemComparer());
            newMetadata.MetaDataItems.ToList().Sort(new FileMetaDataItemComparer());

            //Get newly created items by comparing relative paths
            //newOnly is metadata item in current metadata but not in old one
            IEnumerable<FileMetaDataItem> newOnly = from _new in newMetadata.MetaDataItems
                                                    where !oldMetadata.MetaDataItems.Contains(_new, new FileMetaDataItemComparer())
                                                    select _new;

            //Get deleted items           
            IEnumerable<FileMetaDataItem> oldOnly = from old in oldMetadata.MetaDataItems
                                                    where !newMetadata.MetaDataItems.Contains(old, new FileMetaDataItemComparer())
                                                    select old;

            //get the items from 2 metadata with same relative paths but different hashes.
            IEnumerable<FileMetaDataItem> bothModified = from _new in newMetadata.MetaDataItems
                                                         from old in oldMetadata.MetaDataItems
                                                         where _new.RelativePath.Equals((old).RelativePath)
                                                         && !_new.HashCode.Equals(old.HashCode)
                                                         select _new;

            SQLiteAccess db = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);
            using (SqliteConnection con = db.NewSQLiteConnection())
            {
                SqliteTransaction transaction = (SqliteTransaction)con.BeginTransaction();
                try
                {
                    this.Add(newOnly.ToList(), con);
                    this.Delete(oldOnly.ToList(), con);
                    this.Update(bothModified.ToList(), con);
                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    return false;
                }
            }
            return true;
        }

        public override bool Update(IList<FileMetaDataItem> items)
        {
            SQLiteAccess db = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);
            using (SqliteConnection con = db.NewSQLiteConnection())
            {
                SqliteTransaction trasaction = (SqliteTransaction)con.BeginTransaction();
                try
                {
                    foreach (FileMetaDataItem item in items)
                    {
                        string cmdText = "UPDATE " + Configuration.TBL_METADATA +
                                         " SET " + Configuration.COL_HASH_CODE + " = @hash, " +
                                         Configuration.COL_LAST_MODIFIED_TIME + " = @lmf" +
                                         " WHERE " + Configuration.COL_RELATIVE_PATH + " = @rel AND " +
                                         Configuration.COL_SOURCE_ID + " = @sourceId";

                        SqliteParameterCollection paramList = new SqliteParameterCollection();
                        paramList.Add(new SqliteParameter("@hash", DbType.String) { Value = item.HashCode });
                        paramList.Add(new SqliteParameter("@lmf", DbType.DateTime) { Value = item.LastModifiedTime });
                        paramList.Add(new SqliteParameter("@rel", DbType.String) { Value = item.RelativePath });
                        paramList.Add(new SqliteParameter("@sourceId", DbType.String) { Value = item.SourceId });

                        db.ExecuteNonQuery(cmdText, false);
                    }
                    trasaction.Commit();
                }
                catch (Exception)
                {
                    trasaction.Rollback();
                    return false;
                }
            }

            return true;
        }
        #endregion Update Metadata


        #region Create Schema
        /// <summary>
        /// Create default schema of File metadata table and folder metadata table
        /// No transactional feature
        /// </summary>
        public override void CreateSchema()
        {
            SQLiteAccess db = new SQLiteAccess(Path.Combine(this.StoragePath, Configuration.DATABASE_NAME),true);
            using (SqliteConnection con = db.NewSQLiteConnection())
            {
                string cmdText = "CREATE TABLE IF NOT EXISTS " + Configuration.TBL_METADATA +
                                                " ( " + Configuration.COL_SOURCE_ID + " TEXT, " +
                                                Configuration.COL_RELATIVE_PATH + " TEXT, " +
                                                Configuration.COL_HASH_CODE + " TEXT, " +
                                                Configuration.COL_LAST_MODIFIED_TIME + " DATETIME, " +
                                                Configuration.COL_NTFS_ID1 + " INT, " +
                                                Configuration.COL_NTFS_ID2 + " INT," +
                                                "FOREIGN KEY (" + Configuration.COL_SOURCE_ID + ") REFERENCES " + Configuration.TBL_DATASOURCE_INFO + "(" + Configuration.COL_SOURCE_ID + ")" +
                                                "PRIMARY KEY (" + Configuration.COL_SOURCE_ID + "," + Configuration.COL_RELATIVE_PATH + ")" +
                                                ")";

                db.ExecuteNonQuery(cmdText, false);
            }
        }

        /// <summary>
        /// Create default schema of File metadata table and folder metadata table
        /// with transactional feature
        /// </summary>
        /// <param name="con"></param>
        public void CreateSchema(SqliteConnection con)
        {
            using (SqliteCommand cmd = con.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + Configuration.TBL_METADATA +
                                                " ( " + Configuration.COL_SOURCE_ID + " TEXT, " +
                                                Configuration.COL_RELATIVE_PATH + " TEXT, " +
                                                Configuration.COL_HASH_CODE + " TEXT, " +
                                                Configuration.COL_LAST_MODIFIED_TIME + " DATETIME, " +
                                                Configuration.COL_NTFS_ID1 + " INT, " +
                                                Configuration.COL_NTFS_ID2 + " INT," +
                                                "FOREIGN KEY (" + Configuration.COL_SOURCE_ID + ") REFERENCES " + Configuration.TBL_DATASOURCE_INFO + "(" + Configuration.COL_SOURCE_ID + ")" +
                                                "PRIMARY KEY (" + Configuration.COL_SOURCE_ID + "," + Configuration.COL_RELATIVE_PATH + ")" +
                                                ")";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "CREATE TABLE IF NOT EXISTS " + Configuration.TLB_FOLDERMETADATA +
                    "( " + Configuration.COL_SOURCE_ID + " TEXT, " + Configuration.COL_FOLDER_RELATIVE_PATH + " TEXT )"
                    ;
                cmd.ExecuteNonQuery();
            }
        }
        #endregion Create Schema
    }

}