using Amazon;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public class AwsParameterStoreClient
    {
        private readonly RegionEndpoint _region;

        public AwsParameterStoreClient(RegionEndpoint region)
        {
            _region = region ?? throw new ArgumentNullException(nameof(region));
        }

        public async Task<string> GetValueAsync(string parameter)
        {
            var ssmClient = new AmazonSimpleSystemsManagementClient(_region);
            var response = await ssmClient.GetParameterAsync(new GetParameterRequest
            {
                Name = parameter,
                WithDecryption = true
            });

            return response.Parameter.Value;
        }

        public string GetValue(string parameter)
        {
            var ssmClient = new AmazonSimpleSystemsManagementClient(_region);
            var response = ssmClient.GetParameterAsync(new GetParameterRequest
            {
                Name = parameter,
                WithDecryption = true
            }).Result;

            return response.Parameter.Value;
        }
    }
}
