﻿using System;
namespace BrandingArcServices.Models
{
	public class EmailModel
	{
        public string Email { get; set; }
        public string? otp { get; set; }
        public string? password { get; set; }
        public string? logintype { get; set; }
        public string? user { get; set; }
    }
}

