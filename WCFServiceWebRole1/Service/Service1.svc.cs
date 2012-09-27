using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using WCFServiceWebRole1.Database;
using WCFServiceWebRole1.Helpers;
using System.Net;
using System.IO;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace WCFServiceWebRole1
{
    public class Service1 : IService1
    {
        public string GetHello()
        {            

            // Retrieve storage account from connection string
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create the table client
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            // Create the table if it doesn't exist
            string tableName = "users1";
            tableClient.CreateTableIfNotExist(tableName);

            // Get the data service context
            TableServiceContext serviceContext = tableClient.GetDataServiceContext();            

            return "Hello from my WCF service in Windows Azure!";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loginUrl">Login url</param>
        /// <param name="userName">User's login name</param>
        /// <param name="password">User's password</param>
        /// <param name="formParams">Optional string which contains any additional form params</param>
        /// <returns></returns>
        public bool LoginToSite(String userName, String password)
        {
            //////////////////////////////////////////////////////////////////////////////
            //
            // local variables
            // TODO: Review if there is a better solution to hard-coding these
            //             
            string formParams = string.Format("language=en&affiliateName=espn&parentLocation=&registrationFormId=espn");
            string loginUrl = "https://r.espn.go.com/members/util/loginUser";
            CookieCollection cookies;
            string leagueId = "";
            string teamId = "";
            string season = "";
            string userTable = "ausers00";
            string teamTable = "ateams00";
            string cookieTable = "acookies00";
            bool newUser = true;
            

            /////////////////////////////////////////////////////////////////////////////
            //
            // Lookup / Store user in storage.
            //
            // Encrypt the user password for safe persistence
            // TODO: Store the key somewhere safe. Also, it may be better to store password on the device?
            //
            string passwordHash = Crypto.EncryptStringAES(password, "foobar");

            // Retrieve storage account from connection string
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                CloudConfigurationManager.GetSetting("StorageConnectionString"));

            //
            // Create the table client
            //
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            //
            // Create the user/team/cookie tables if they don't exist            
            //
            tableClient.CreateTableIfNotExist(userTable);       
            tableClient.CreateTableIfNotExist(teamTable);
            tableClient.CreateTableIfNotExist(cookieTable);

            // Get the data service context
            TableServiceContext serviceContext = tableClient.GetDataServiceContext();

            
            //            
            // Look up the user in the storage tables
            //            
            UserEntity specificEntity =
                (from e in serviceContext.CreateQuery<UserEntity>(userTable)
                 where e.RowKey == userName
                 select e).FirstOrDefault();


            //
            // Check to see if password is correct
            //
            if (specificEntity != null)
            {
                string userPassword = Crypto.DecryptStringAES(specificEntity.PasswordHash, "foobar");

                if (userPassword == password)
                {
                    newUser = false;
                }
            }


            //
            // If this is a new user add them to the users databse
            //           
            if (newUser)
            {                
                //
                // Setup formUrl string
                //
                string formUrl = "";

                if (!String.IsNullOrEmpty(formParams))
                {
                    formUrl = formParams + "&";
                }
                formUrl += string.Format("username={0}&password={1}", userName, password);


                //
                // Now setup the POST request
                //
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(loginUrl);
                req.CookieContainer = new CookieContainer();
                req.ContentType = "application/x-www-form-urlencoded";
                req.Method = "POST";

                byte[] bytes = Encoding.ASCII.GetBytes(formUrl);
                req.ContentLength = bytes.Length;

                using (Stream os = req.GetRequestStream())
                {
                    os.Write(bytes, 0, bytes.Length);
                }


                //
                // Read the response
                //
                string respString = "";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    //
                    // Gather cookie info
                    //
                    cookies = resp.Cookies;


                    //
                    // Read the response stream to determine if login was successful.
                    //
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                    {
                        Char[] buffer = new Char[256];
                        int count = reader.Read(buffer, 0, 256);
                        while (count > 0)
                        {
                            respString += new String(buffer, 0, count);
                            count = reader.Read(buffer, 0, 256);
                        }

                    } // reader is disposed here

                }

                //writeLog(respString);

                //
                // If response string contains "login":"true" this indicates success
                //
                if (!respString.Contains("{\"login\":\"true\"}"))
                {
                    throw new InvalidOperationException("Failed to login with given credentials");
                }


                //
                // Login success - Now save user to databse
                // TODO: Figure out a better partition key other than "key"
                //
                UserEntity user = new UserEntity("key", userName);
                user.PasswordHash = passwordHash;

                // Add the new team to the teams table
                serviceContext.AddObject(userTable, user);


                //////////////////////////////////////////////////////////////////////////////////
                //
                // Now that we have logged in, extract user info from the page
                //

                string url2 = "http://games.espn.go.com/frontpage/football";

                HttpWebRequest req2 = (HttpWebRequest)WebRequest.Create(url2);
                req2.CookieContainer = new CookieContainer();
                req2.ContentType = "application/x-www-form-urlencoded";
                req2.Method = "POST";

                //
                // Add the login cookies
                //
                foreach (Cookie cook in cookies)
                {
                    //writeLog("**name = " + cook.Name + " value = " + cook.Value);
                    CookieEntity newCook = new CookieEntity(userName, cook.Name);
                    newCook.Value = cook.Value;
                    newCook.Path = cook.Path;
                    newCook.Domain = cook.Domain;

                    // Add the new cookie to the cookies table
                    serviceContext.AddObject(cookieTable, newCook);

                    req2.CookieContainer.Add(cook);
                }


                //
                // Read the response
                //            
                using (HttpWebResponse resp = (HttpWebResponse)req2.GetResponse())
                {

                    //
                    // Do something with the response stream. As an example, we'll 
                    // stream the response to the console via a 256 character buffer 
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                    {
                        HtmlDocument doc = new HtmlDocument();
                        doc.Load(reader);

                        //
                        // Build regEx for extracting legue info from link address
                        //
                        string pattern = @"(clubhouse\?leagueId=)(\d+)(&teamId=)(\d+)(&seasonId=)(\d+)";
                        Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);

                        foreach (HtmlNode hNode in doc.DocumentNode.SelectNodes("//a[@href]"))
                        {

                            HtmlAttribute att = hNode.Attributes["href"];
                            Match match = rgx.Match(att.Value);

                            //
                            // If our regEx finds a match then we have leagueId, teamId, and seasonId
                            //
                            if (match.Success)
                            {
                                //writeLog("NODE> Name: " + hNode.Name + ", ID: " + hNode.Id + "attr: " + att.Value);

                                GroupCollection groups = match.Groups;

                                leagueId = groups[2].Value;
                                teamId = groups[4].Value;
                                season = groups[6].Value;

                                //
                                // Create a new team entity
                                //
                                TeamEntity team = new TeamEntity(leagueId, teamId);

                                team.Season = season;
                                team.UserId = userName;                                

                                // Add the new team to the teams table
                                serviceContext.AddObject(teamTable, team);
                                
                            }

                        }

                    } // reader is disposed here


                }                

                // Submit the operation to the table service
                serviceContext.SaveChangesWithRetries();

            }

            return true;
        }
    }
}
