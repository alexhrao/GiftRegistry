﻿namespace GiftServer
{
    namespace Data
    {
        /// <summary>
        /// Indicates that this class can "synchronize" with the database - it can be created, updated, and deleted from the Database
        /// </summary>
        public interface ISynchronizable
        {
            /// <summary>
            /// Create a record of this in the database
            /// </summary>
            /// <returns>A Status Flag</returns>
            bool Create();
            /// <summary>
            /// Update a record of this in the database
            /// </summary>
            /// <returns>A Status Flag</returns>
            bool Update();
            /// <summary>
            /// Delete the record of this in the database
            /// </summary>
            /// <returns>A Status Flag</returns>
            bool Delete();
        }
    }
}