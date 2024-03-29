using Azure.Identity;
using KeyVaultCa.Core;
using KeyVaultCA.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KeyVaultCA.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();

            var estConfig = Configuration.GetSection("KeyVault").Get<EstConfiguration>();
            services.AddSingleton(estConfig);

            var estAuth = Configuration.GetSection("EstAuthentication").Get<AuthConfiguration>();
            services.AddSingleton(estAuth);

            var azureCredential = new DefaultAzureCredential();
            services.AddSingleton(azureCredential);
            services.AddSingleton<KeyVaultServiceClient>();
            services.AddSingleton<IKeyVaultCertificateProvider, KeyVaultCertificateProvider>();

            services.AddControllers();

            services.AddScoped<IUserService, UserService>();

            if (estAuth.AuthMode == AuthMode.Basic)
            {
                services.AddAuthentication("BasicAuthentication")
                    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
            }
            else if (estAuth.AuthMode == AuthMode.x509)
            {
                services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
                   .AddCertificate(options =>
                   {
                       var trustedCAs = new List<X509Certificate2>();
                       var trustedCADir = Path.Combine(AppContext.BaseDirectory, @"TrustedCAs");
                       foreach (var file in Directory.EnumerateFiles(trustedCADir, "*.cer"))
                       {
                           var contents = File.ReadAllText(file);
                           trustedCAs.Add(X509Certificate2.CreateFromPem(contents));
                       }

                       options.CustomTrustStore.AddRange(new X509Certificate2Collection(trustedCAs.ToArray()));

                       // Azure KeyVault does not support this
                       options.RevocationMode = X509RevocationMode.NoCheck;
                       options.ChainTrustValidationMode = X509ChainTrustMode.CustomRootTrust;

                       options.Events = new CertificateAuthenticationEvents
                       {
                           OnCertificateValidated = context =>
                           {
                               var claims = new[]
                               {
                                        new Claim(
                                            ClaimTypes.NameIdentifier,
                                            context.ClientCertificate.Subject,
                                            ClaimValueTypes.String,
                                            context.Options.ClaimsIssuer),
                                        new Claim(
                                            ClaimTypes.Name,
                                            context.ClientCertificate.Subject,
                                            ClaimValueTypes.String,
                                            context.Options.ClaimsIssuer)
                               };

                               context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                               context.Success();

                               return Task.CompletedTask;
                           }
                       };
                   })
                   .AddCertificateCache();

                services.AddCertificateForwarding(options =>
                {
                    options.CertificateHeader = "X-ARR-ClientCert";
                });

                services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
                    options.ForwardedProtoHeaderName = "X-Forwarded-Proto";
                });
            }

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "KeyVaultCA.Web", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "KeyVaultCA.Web v1"));
            }

            app.UseRouting();
            app.UseCertificateForwarding();
            app.UseForwardedHeaders();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
