﻿using System;
namespace BrandingArcServices.Models
{
	public class CaseRequest
	{
        public List<CaseModel> data { get; set; }
        public List<CaseModel> Data { get; internal set; }
    }
}

