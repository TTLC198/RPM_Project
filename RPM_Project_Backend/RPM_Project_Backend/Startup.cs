﻿using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RPM_Project_Backend.Config;
using RPM_Project_Backend.Helpers;
using RPM_Project_Backend.Services.Database;

namespace RPM_Project_Backend;

public class Startup
{
    private IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
        
    }
    
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(name: "_MyPolicy", policy => policy.WithOrigins("http://localhost:3000").AllowCredentials().AllowAnyMethod());
        });
        
        var connection = Configuration.GetConnectionString("DefaultConnection")!;
        services.AddMvc();
        services.AddDbContext<ApplicationContext>(options => options.UseSqlServer(connection));
        services.AddControllers(options => options.AllowEmptyInputInBodyModelBinding = true);
        services.AddControllers().AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.Converters.Add(new JsonDateTimeConverter());
            opt.JsonSerializerOptions.DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull;
            opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opt.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        // Use Swagger
        // Inject an implementation of ISwaggerProvider with defaulted settings applied.
        services.AddSwaggerGen();
        services.AddSwaggerConfiguration();
        
        services.AddJwtConfiguration(Configuration);
        services.AddAutoMapperConfiguration();
    }
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Use Swagger
        app.UseSwaggerUI(c => {
            c.RoutePrefix = "swagger/ui";
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "RPM_Project_APi v1");
        });
        
        // Enable middleware to serve generated Swagger as a JSON endpoint
        app.UseSwagger();
        // Enable middleware to serve swagger-ui assets (HTML, JS, CSS etc.)
        app.UseSwaggerUI();
        app.UseSwaggerConfiguration();

        app.UseStaticFiles();
        
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseHttpsRedirection();
        
        app.UseAuthentication();
        app.UseRouting();
        app.UseCors("_MyPolicy");
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}