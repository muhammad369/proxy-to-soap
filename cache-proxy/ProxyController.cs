using cache_proxy.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace cache_proxy
{
    
    public class ProxyController : ApiController
    {

        [HttpGet, HttpPost, HttpHead, HttpOptions, HttpPut, HttpPatch]
        public async Task<HttpResponseMessage> Proxy([FromUri]string url)
        {
            using (var http = new HttpClient())
            {
                this.Request.RequestUri = new Uri(url);
                string reqContent="";
                var method= this.Request.Method;

                if (method == HttpMethod.Get)
                {
                    this.Request.Content = null;
                }
                else
                {
                    reqContent = await this.Request.Content.ReadAsStringAsync();
                }

                string cachedResp = null;
                var sa= soapAction(this.Request);

                if (Program.useCache)
                {
                    cachedResp = getCachedResponse(url, method.Method, sa, reqContent);
                }

                
                HttpResponseMessage resp= null;

                if (cachedResp == null)
                {
                    //send the request
                    resp = await http.SendAsync(this.Request);

                    cachedResp = await resp.Content.ReadAsStringAsync();
                    //
                    //save to cache
                    saveToLog(url,method.Method, sa, reqContent, cachedResp, resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.MethodNotAllowed);
                    log(url, sa, false, method.Method);
                }
                else //cached
                {
                    log(url, sa, true, method.Method);
                }
                

                //wrap the response and return it
                var response = new HttpResponseMessage
                {
                    //StatusCode= resp.StatusCode,
                    Content =
                        new StringContent(cachedResp, Encoding.UTF8, "application/xml")
                };

                return response;
            }
        }


        private string getCachedResponse(string url, string method, string soapAction, string request)
        {
            using (var db = new dbContext())
            {
                var reqs= db.requests.Where(q => q.url == url && q.soap_action == soapAction && q.requestContent == request && q.method==method);
                if (reqs.Count() == 0)
                {
                    return null;
                }
                var req = reqs.First();

                return req.response;
            }

        }

        private string soapAction(HttpRequestMessage req)
        {
            var hdrs = this.Request.Headers.Where(h => h.Key == "SOAPAction");
            if(hdrs.Count() ==0)
            {
                return "";
            }
            var hd = hdrs.First();
            return hd.Value.First().ToString();
        }

        private void saveToLog(string url, string method, string soapAction, 
                               string reqContent, string stringResp, bool success)
        {
            if (!success) return;

            using (var db = new dbContext())
            {
                var reqs= db.requests.Where(q => q.url == url && q.soap_action == soapAction && q.requestContent == reqContent && q.method==method);
                if (reqs.Count() > 0)
                {
                    var req = reqs.First();
                    req.response = stringResp;
                    req.time = DateTime.Now;
                }
                else
                {
                    var req = new tbl_request()
                    {
                        url = url,
                        method = method,
                        requestContent = reqContent,
                        soap_action = soapAction,
                        response = stringResp,
                        time = DateTime.Now
                    };
                    db.requests.Add(req);
                }
                db.SaveChanges();
            }
        }

        private void log(string url, string action, bool cache, string method)
        {
            Form1.list.Insert(0, new LogItem() {
                url= url,
                action= action,
                fromCache= cache,
                method= method,
                time= DateTime.Now
            });
        }


    }
}
