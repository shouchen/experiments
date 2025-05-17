using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Threading.Tasks;

//namespace ExportCmdlet
//{
//    public class AuthenticatedHttpClientHandler : DelegatingHandler
//    {
//        private readonly ITokenService _tokenService;

//        public AuthenticatedHttpClientHandler(ITokenService tokenService)
//        {
//            _tokenService = tokenService;
//        }

//        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
//        {
//            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenService.GetAccessToken());

//            var response = await base.SendAsync(request, cancellationToken);

//            if (response.StatusCode == HttpStatusCode.Unauthorized)
//            {
//                _tokenService.RefreshToken();
//                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenService.GetAccessToken());
//                response = await base.SendAsync(request, cancellationToken);
//            }

//            return response;
//        }
//    }
//}
