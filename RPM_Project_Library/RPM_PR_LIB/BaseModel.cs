﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPM_PR_LIB;

public abstract class BaseModel
{
    public abstract long Id { get; set; }
}