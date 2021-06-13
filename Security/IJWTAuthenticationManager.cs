using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dotnetcoreJWT.Dto;
using dotnetcoreJWT.Models;

namespace dotnetcoreJWT.Security
{
    public interface IJWTAuthenticationManager
    {
        string Authenticate(User loginRequestModel); 
    }
}
