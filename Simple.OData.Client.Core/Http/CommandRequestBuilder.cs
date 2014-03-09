﻿using System;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Simple.OData.Client
{
    class CommandRequestBuilder : RequestBuilder
    {
        public CommandRequestBuilder(string urlBase, ICredentials credentials)
            : base(urlBase, credentials)
        {
        }

        public override HttpRequest CreateRequest(HttpCommand command, bool returnContent = false)
        {
            var uri = CreateRequestUrl(command.CommandText);
            var request = CreateRequest(uri);
            request.Method = command.Method;
            request.ReturnContent = returnContent;

            // TODO: revise
            //if (method == "PUT" || method == "DELETE" || method == "MERGE")
            //{
            //    request.Headers.Add("If-Match", "*");
            //}

            if (command.FormattedContent != null)
            {
                request.ContentType = command.ContentType;
                request.Content = new StringContent(command.FormattedContent, Encoding.UTF8, command.ContentType);
            }
            else if (!command.ReturnsScalarResult)
            {
                request.Accept = new[] { "application/text", "application/xml", "application/atom+xml" };
            }

            return request;
        }

        public override int GetContentId(object content)
        {
            return 0;
        }
    }
}