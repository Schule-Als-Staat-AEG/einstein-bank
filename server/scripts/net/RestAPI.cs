using RestSharp;
using Newtonsoft.Json;
using System.Dynamic;

namespace Max35050.Net;

public class RestCall
{
    public string endpoint;
    public string authToken;
    public string body;
    public bool expectError;
    public RestCall(string endpoint, string authToken = "", string body = "", bool expectError = false)
    {
        this.endpoint = endpoint;
        this.authToken = authToken;
        this.body = body;
        this.expectError = expectError;
    }
    public async Task<RestResponse> GET()
    {
        var client = new RestClient(endpoint);
        var request = new RestRequest();
        authToken = authToken.Trim();
        Debug.LogConnection($"Making GET request to {endpoint} with authToken: {(string.IsNullOrEmpty(authToken) ? "No Token" : "Provided")}");
        Debug.LogConnection($"GET Request Body: {body}");
        Debug.LogConnection($"GET Request Headers: Content-Type: application/json, Authorization: {(string.IsNullOrEmpty(authToken) ? "None" : "Bearer " + authToken)}");
        request.AddHeader("Content-Type", "application/json");
        if (authToken != "")
        {
            request.AddHeader("Authorization", "Bearer " + authToken);
        }
        try
        {
            RestResponse response = await client.GetAsync(request);
            Debug.LogConnection($"GET Response Status: {response.StatusCode}");
            Debug.LogConnection($"GET Response Content: {response.Content}");
            return response;
        }
        catch
        {
            if (!expectError)
            {
                //Debug.LogError("GET request failed: " + ex.Message);
                //Debug.LogError($"Endpoint: {endpoint}");
            }
            return new RestResponse { StatusCode = System.Net.HttpStatusCode.ServiceUnavailable, Content = "" };
        }
    }
    public async Task<RestResponse> POST()
    {
        var client = new RestClient(endpoint);
        var request = new RestRequest();
        authToken = authToken.Trim();
        Debug.LogConnection($"Making POST request to {endpoint} with authToken: {(string.IsNullOrEmpty(authToken) ? "No Token" : "Provided")}");
        Debug.LogConnection($"POST Request Body: {body}");
        Debug.LogConnection($"POST Request Headers: Content-Type: application/json, Authorization: {(string.IsNullOrEmpty(authToken) ? "None" : "Bearer " + authToken)}");
        request.AddHeader("Content-Type", "application/json");
        if (authToken != "")
        {
            request.AddHeader("Authorization", "Bearer " + authToken);
        }
        request.AddJsonBody(body);
        try
        {
            RestResponse response = await client.PostAsync(request);
            Debug.LogConnection($"POST Response Status: {response.StatusCode}");
            Debug.LogConnection($"POST Response Content: {response.Content}");
            return response;
        }
        catch
        {
            if (!expectError)
            {
                //Debug.LogError("POST request failed: " + ex.Message);
                //Debug.LogError($"Endpoint: {endpoint}");
            }
            return new RestResponse { StatusCode = System.Net.HttpStatusCode.ServiceUnavailable, Content = "" };
        }
    }
    public async Task<RestResponse> PUT()
    {
        var client = new RestClient(endpoint);
        var request = new RestRequest();
        authToken = authToken.Trim();
        Debug.LogConnection($"Making PUT request to {endpoint} with authToken: {(string.IsNullOrEmpty(authToken) ? "No Token" : "Provided")}");
        Debug.LogConnection($"PUT Request Body: {body}");
        Debug.LogConnection($"PUT Request Headers: Content-Type: application/json, Authorization: {(string.IsNullOrEmpty(authToken) ? "None" : "Bearer " + authToken)}");
        request.AddHeader("Content-Type", "application/json");
        if (authToken != "")
        {
            request.AddHeader("Authorization", "Bearer " + authToken);
        }
        request.AddJsonBody(body);
        try
        {
            RestResponse response = await client.PutAsync(request);
            Debug.LogConnection($"PUT Response Status: {response.StatusCode}");
            Debug.LogConnection($"PUT Response Content: {response.Content}");
            return response;
        }
        catch
        {
            if (!expectError)
            {
                //Debug.LogError("PUT request failed: " + ex.Message);
                //Debug.LogError($"Endpoint: {endpoint}");
            }
            return new RestResponse { StatusCode = System.Net.HttpStatusCode.ServiceUnavailable, Content = "" };
        }
    }
    public async Task<RestResponse> DELETE()
    {
        var client = new RestClient(endpoint);
        var request = new RestRequest();
        request.AddHeader("Content-Type", "application/json");
        authToken = authToken.Trim();
        Debug.LogConnection($"Making DELETE request to {endpoint} with authToken: {(string.IsNullOrEmpty(authToken) ? "No Token" : "Provided")}");
        Debug.LogConnection($"DELETE Request Body: {body}");
        Debug.LogConnection($"DELETE Request Headers: Content-Type: application/json, Authorization: {(string.IsNullOrEmpty(authToken) ? "None" : "Bearer " + authToken)}");
        if (authToken != "")
        {
            request.AddHeader("Authorization", "Bearer " + authToken);
        }
        try
        {
            RestResponse response = await client.DeleteAsync(request);
            Debug.LogConnection($"DELETE Response Status: {response.StatusCode}");
            Debug.LogConnection($"DELETE Response Content: {response.Content}");
            return response;
        }
        catch
        {
            if (!expectError)
            {
                //Debug.LogError("DELETE request failed: " + ex.Message);
                //Debug.LogError($"Endpoint: {endpoint}");
            }
            return new RestResponse { StatusCode = System.Net.HttpStatusCode.ServiceUnavailable, Content = "" };
        }
    }
    public async Task<RestResponse> PATCH()
    {
        var client = new RestClient(endpoint);
        var request = new RestRequest();
        request.AddHeader("Content-Type", "application/json");
        authToken = authToken.Trim();
        Debug.LogConnection($"Making PATCH request to {endpoint} with authToken: {(string.IsNullOrEmpty(authToken) ? "No Token" : "Provided")}");
        Debug.LogConnection($"PATCH Request Body: {body}");
        Debug.LogConnection($"PATCH Request Headers: Content-Type: application/json, Authorization: {(string.IsNullOrEmpty(authToken) ? "None" : "Bearer " + authToken)}");
        if (authToken != "")
        {
            request.AddHeader("Authorization", "Bearer " + authToken);
        }
        request.AddJsonBody(body);
        try
        {
            RestResponse response = await client.PatchAsync(request);
            Debug.LogConnection($"PATCH Response Status: {response.StatusCode}");
            Debug.LogConnection($"PATCH Response Content: {response.Content}");
            return response;
        }
        catch
        {
            if (!expectError)
            {
                //Debug.LogError("PATCH request failed: " + ex.Message);
                //Debug.LogError($"Endpoint: {endpoint}");
            }
            return new RestResponse { StatusCode = System.Net.HttpStatusCode.ServiceUnavailable, Content = "" };
        }
    }

    public async Task<dynamic> GetDynamic()
    {
        try
        {
            var response = await GET();
            if (response == null || string.IsNullOrEmpty(response.Content)) return new ExpandoObject();
            return JsonConvert.DeserializeObject<dynamic>(response.Content)!;
        }
        catch
        {
            //Debug.LogError("GET deserialize failed: " + ex.Message);
            //Debug.LogError($"Endpoint: {endpoint}");
            return new ExpandoObject();
        }
    }

    public async Task<dynamic> PostDynamic()
    {
        try
        {
            var response = await POST();
            if (response == null || string.IsNullOrEmpty(response.Content)) return new ExpandoObject();
            return JsonConvert.DeserializeObject<dynamic>(response.Content)!;
        }
        catch
        {
            //Debug.LogError("POST deserialize failed: " + ex.Message);
            //Debug.LogError($"Endpoint: {endpoint}");
            return new ExpandoObject();
        }
    }

    public async Task<dynamic> PutDynamic()
    {
        try
        {
            var response = await PUT();
            if (response == null || string.IsNullOrEmpty(response.Content)) return new ExpandoObject();
            return JsonConvert.DeserializeObject<dynamic>(response.Content)!;
        }
        catch
        {
            //Debug.LogError("PUT deserialize failed: " + ex.Message);
            //Debug.LogError($"Endpoint: {endpoint}");
            return new ExpandoObject();
        }
    }

    public async Task<dynamic> DeleteDynamic()
    {
        try
        {
            var response = await DELETE();
            if (response == null || string.IsNullOrEmpty(response.Content)) return new ExpandoObject();
            return JsonConvert.DeserializeObject<dynamic>(response.Content)!;
        }
        catch
        {
            //Debug.LogError("DELETE deserialize failed: " + ex.Message);
            //Debug.LogError($"Endpoint: {endpoint}");
            return new ExpandoObject();
        }
    }

    public async Task<dynamic> PatchDynamic()
    {
        try
        {
            var response = await PATCH();
            if (response == null || string.IsNullOrEmpty(response.Content)) return new ExpandoObject();
            return JsonConvert.DeserializeObject<dynamic>(response.Content)!;
        }
        catch 
        {
            //Debug.LogError("PATCH deserialize failed: " + ex.Message);
            //Debug.LogError($"Endpoint: {endpoint}");
            return new ExpandoObject();
        }
    }
}