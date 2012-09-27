using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.StorageClient;

namespace WCFServiceWebRole1.Database
{    
    public class TeamEntity : TableServiceEntity
    {
        public TeamEntity(string leagueId, string teamId)
        {
            //
            // For Azure table storage to load balance properly
            // we need to define both a partition key as well as a row key.
            //
            this.PartitionKey = leagueId;
            this.RowKey = teamId;
        }

        public TeamEntity() { }

        public string UserId { get; set; }

        public string Season { get; set; }        
    }

}