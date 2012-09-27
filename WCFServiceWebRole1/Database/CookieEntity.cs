using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.StorageClient;

namespace WCFServiceWebRole1.Database
{
    public class CookieEntity : TableServiceEntity
    {
        public CookieEntity(string userId, string name)
        {
            //
            // For Azure table storage to load balance properly
            // we need to define both a partition key as well as a row key.
            //
            this.PartitionKey = userId;
            this.RowKey = name;
        }

        public CookieEntity() { }

        public string Value { get; set; }

        public string Path { get; set; }

        public string Domain { get; set; }

    }

}