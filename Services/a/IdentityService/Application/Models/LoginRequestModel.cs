﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Application.Models
{
    public class LoginRequestModel
    {
        public string UserName { get; set; }

        public string Password { get; set; }

    }
}