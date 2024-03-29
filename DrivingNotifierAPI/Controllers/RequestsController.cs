﻿using DrivingNotifierAPI.Data;
using DrivingNotifierAPI.Models;
using DrivingNotifierAPI.Utilities;
using Mailjet.Client;
using Mailjet.Client.Resources;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DrivingNotifierAPI.Controllers
{
    [Produces("application/json")]
    [Route("api/Requests")]
    public class RequestsController : Controller
    {
        private readonly DataAccessRequest dataRequest;
        private readonly DataAccessUser dataUser;

        public RequestsController(DataAccessRequest dataForRequest, DataAccessUser dataForUser)
        {
            dataRequest = dataForRequest;
            dataUser = dataForUser;
        }

        // GET: api/Requests
        [HttpGet]
        public Task<IEnumerable<Request>> GetRequests()
        {
            return dataRequest.GetRequests();
        }

        // GET: api/Requests/requestor@mail.com/replier@mail.com
        [HttpGet("{requestorEmail}/{replierEmail}", Name = "getRequestByEmails")]
        public IActionResult GetRequest([FromRoute] string requestorEmail, [FromRoute] string replierEmail)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var request = dataRequest.GetRequest(requestorEmail, replierEmail);

            if (request == null)
            {
                return NotFound();
            }

            return Ok(request);
        }

        // GET: api/PendingRequests/abcsfds@mail.com
        [HttpGet("PendingRequests/{email}")]
        public Task<IEnumerable<Request>> GetPendingRequests([FromRoute] string email)
        {
            return dataRequest.GetPendingRequests(email);
        }

        // GET: api/PendingRequests/Accept/23453294
        [HttpGet("PendingRequests/Accept/{requestId}")]
        public Task AcceptPendingRequests([FromRoute] string requestId)
        {
            return dataRequest.AcceptRequest(requestId);
        }

        // GET: api/PendingRequests/Decline/23453294
        [HttpGet("PendingRequests/Decline/{requestId}")]
        public Task DeclinePendingRequests([FromRoute] string requestId)
        {
            return dataRequest.DeclineRequest(requestId);
        }

        // POST: api/Requests
        [HttpPost]
        public async Task<IActionResult> PostRequest([FromBody] Request request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // I check that there exists a user with the replier email of the request. If it doesn't return 400 fx.
            var replierUser = dataUser.GetUserByEmail(request.RequestorEmail);
            if (replierUser == null)
            {
                return NotFound();
            }

            //We only add a new request if that does not already exist
            var requestFetched = dataRequest.GetRequest(request.RequestorEmail, request.ReplierEmail);
            var requestorUser = dataUser.GetUserByEmail(request.RequestorEmail);
            if (requestFetched == null && !requestorUser.Contacts.Contains(replierUser.Id))
            {
                request.RequestorUsername = requestorUser.FullName;
                await dataRequest.CreateRequest(request);

                await SendRequestEmail.SendAcceptanceEmail(request);

                return Ok(request); //TODO change for other // https://github.com/Microsoft/aspnet-api-versioning/issues/18
            }

            return BadRequest(ModelState);
        }

        [HttpPut]
        public async Task<IActionResult> PutRequest([FromBody] Request request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var requestFetched = dataRequest.GetRequest(request.RequestorEmail, request.ReplierEmail);
            if (requestFetched != null)
            {
                await dataRequest.UpdateRequestState(request.RequestorEmail, request.ReplierEmail, request.State);

                // Add the ObjectId to the Contacts lists of the user, in case request is accepted.
                if (request.State.Equals(RequestState.ACCEPTED))
                {
                    //Update the list of the replier in the database.
                    await dataUser.AddUserContactList(requestFetched.RequestorEmail, requestFetched.ReplierEmail);
                } else if (request.State.Equals(RequestState.DENIED))  //We delete the request.
                {
                    await dataRequest.DeleteRequest(requestFetched.RequestorEmail, requestFetched.ReplierEmail);
                }

            } else
            {
                return NotFound();
            }

            return Ok(request);
        }

        // DELETE: api/Requests/requestor@mail.com/replier@mail.com
        [HttpDelete("{requestorEmail}/{replierEmail}")]
        public async Task<IActionResult> DeleteRequest([FromRoute] string requestorEmail, [FromRoute] string replierEmail)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var request = dataRequest.GetRequest(requestorEmail, replierEmail);
            if (request == null)
            {
                return NotFound();
            }

            await dataRequest.DeleteRequest(requestorEmail, replierEmail);

            return Ok();
        }
    }
}
