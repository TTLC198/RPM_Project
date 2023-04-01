﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace RPM_PR_LIB;


public partial class ProductsHaveAttribute : BaseModel
{
    [Column("pha_id")]
    public override int Id { get; set; }

    [Column("pha_pro_id")]
    public int ProductId { get; set; }
    [ValidateNever]
    public virtual Product Product { get; set; } = null!;

    [Column("pha_atr_id")]
    public int AttributeId { get; set; }
    [ValidateNever]
    public virtual Attribute Attribute { get; set; } = null!;
    
    [Column("pha_value")]
    public string Value { get; set; } = null!;
}
