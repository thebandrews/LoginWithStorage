using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.StorageClient;

namespace WCFServiceWebRole1.Database
{
    public class UserEntity : TableServiceEntity
    {
        public UserEntity(string key, string loginId)
        {
            //
            // For Azure table storage to load balance properly
            // we need to define both a partition key as well as a row key.
            //
            this.PartitionKey = key;
            this.RowKey = loginId;
        }

        public UserEntity() { }

        public string PasswordHash { get; set; }
        
    }   

}