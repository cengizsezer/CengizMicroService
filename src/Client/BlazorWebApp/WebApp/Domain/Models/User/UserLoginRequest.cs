﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApp.Domain.Models.User
{
    public class UserLoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string RefreshToken { get; set; }


    }
}