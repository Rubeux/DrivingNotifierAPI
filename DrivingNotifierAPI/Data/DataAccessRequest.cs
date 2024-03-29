﻿using DrivingNotifierAPI.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DrivingNotifierAPI.Data
{
    public class DataAccessRequest
    {
        private MongoClient client;
        private IMongoDatabase db;
        private readonly string DB_COLLECTION_NAME_REQUESTS = "Requests";
        private readonly string DB_NAME = "DrivingNotifier";
        //private readonly string DB_CLIENT_URL_LOCAL = "mongodb://localhost:27017";
        private readonly string DB_CLIENT_URL_REMOTE = "mongodb://dnadmin:"+ PrivateCredentials.PASS_DB_REMOTE +"@" +
            "drivingnotifier-shard-00-00-i0wld.mongodb.net:27017," +
            "drivingnotifier-shard-00-01-i0wld.mongodb.net:27017," +
            "drivingnotifier-shard-00-02-i0wld.mongodb.net:27017/test?" +
            "ssl=true&replicaSet=DrivingNotifier-shard-0&authSource=admin&retryWrites=true";

        public DataAccessRequest()
        {
            client = new MongoClient(DB_CLIENT_URL_REMOTE);
            db = client.GetDatabase(DB_NAME);
        }

        public async Task<IEnumerable<Request>> GetRequests()
        {
            return await db.GetCollection<Request>(DB_COLLECTION_NAME_REQUESTS).Find(_ => true).ToListAsync();
        }

        public Request GetRequest(String id)
        {
            var filter = Builders<Request>.Filter.Eq(u => u.IdEntity, id);

            return db.GetCollection<Request>(DB_COLLECTION_NAME_REQUESTS).Find(filter).FirstOrDefault();
        }

        public Request GetRequest(string requestorEmail, string replierEmail)
        {
            var filter = Builders<Request>.Filter.And(
                Builders<Request>.Filter.Eq(u => u.ReplierEmail, replierEmail), 
                Builders<Request>.Filter.Eq(u => u.RequestorEmail, requestorEmail),
                Builders<Request>.Filter.Eq(u => u.State, RequestState.PENDING));

            return db.GetCollection<Request>(DB_COLLECTION_NAME_REQUESTS).Find(filter).FirstOrDefault();
        }

        public async Task CreateRequest(Request request)
        {
            Random random = new Random();
            int num = random.Next();
            string hexString = num.ToString("X2");
            string hexValue = DateTime.Now.Ticks.ToString("X2");
            request.IdEntity = hexValue+hexString;
            request.State = RequestState.PENDING;
            await db.GetCollection<Request>(DB_COLLECTION_NAME_REQUESTS).InsertOneAsync(request);
        }

        public async Task<IEnumerable<Request>> GetPendingRequests(string currentEmail)
        {
            var filter = Builders<Request>.Filter.And(
                Builders<Request>.Filter.Eq(u => u.ReplierEmail, currentEmail), 
                Builders<Request>.Filter.Eq(u => u.State, RequestState.PENDING));

            return await db.GetCollection<Request>(DB_COLLECTION_NAME_REQUESTS).Find(filter).ToListAsync();
        }


        public async Task AcceptRequest(string id)
        {
            var filter = Builders<Request>.Filter.And(
                Builders<Request>.Filter.Eq(u => u.IdEntity, id),
                Builders<Request>.Filter.Eq(u => u.State, RequestState.PENDING));
            var update = Builders<Request>.Update.Set(s => s.State, RequestState.ACCEPTED);
            var request = GetRequest(id);
            var dataUser = new DataAccessUser();
            await dataUser.AddUserContactList(request.RequestorEmail, request.ReplierEmail);
            await db.GetCollection<Request>(DB_COLLECTION_NAME_REQUESTS).UpdateOneAsync(filter, update);
        }

        public async Task DeclineRequest(string id)
        {
            var filter = Builders<Request>.Filter.And(
                Builders<Request>.Filter.Eq(u => u.IdEntity, id),
                Builders<Request>.Filter.Eq(u => u.State, RequestState.PENDING));
            var update = Builders<Request>.Update.Set(s => s.State, RequestState.DENIED);
            await db.GetCollection<Request>(DB_COLLECTION_NAME_REQUESTS).UpdateOneAsync(filter, update);
        }

        public async Task UpdateRequestState(string requestorEmail, string replierEmail, RequestState state)
        {
            var filter = Builders<Request>.Filter.And(
                Builders<Request>.Filter.Eq(u => u.ReplierEmail, replierEmail),
                Builders<Request>.Filter.Eq(u => u.RequestorEmail, requestorEmail));
            var update = Builders<Request>.Update.Set(s => s.State, state);

            await db.GetCollection<Request>(DB_COLLECTION_NAME_REQUESTS).UpdateOneAsync(filter, update);
        }

        public async Task UpdateRequestHexCode(string requestorEmail, string replierEmail, string hexCode)
        {
            var filter = Builders<Request>.Filter.And(
                Builders<Request>.Filter.Eq(u => u.ReplierEmail, replierEmail),
                Builders<Request>.Filter.Eq(u => u.RequestorEmail, requestorEmail));
            var update = Builders<Request>.Update.Set(s => s.IdEntity, hexCode);

            await db.GetCollection<Request>(DB_COLLECTION_NAME_REQUESTS).UpdateOneAsync(filter, update);
        }

        public async Task DeleteRequest(string requestorEmail, string replierEmail)
        {
            var filter = Builders<Request>.Filter.And(
                Builders<Request>.Filter.Eq(u => u.ReplierEmail, replierEmail),
                Builders<Request>.Filter.Eq(u => u.RequestorEmail, requestorEmail));

            await db.GetCollection<Request>(DB_COLLECTION_NAME_REQUESTS).DeleteOneAsync(filter);
        }
    }
}
