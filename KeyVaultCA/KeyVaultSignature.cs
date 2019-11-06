// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Org.BouncyCastle.Asn1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace KeyVaultCA
{
    public class KeyVaultCertFactory
    {
        public const int SerialNumberLength = 20;
        public const int DefaultKeySize = 2048;

        /// <summary>
        /// Creates a KeyVault signed certificate.
        /// </summary>
        /// <returns>The signed certificate</returns>
        public static Task<X509Certificate2> CreateSignedCertificate(
            string subjectName,
            ushort keySize,
            DateTime notBefore,
            DateTime notAfter,
            ushort hashSizeInBits,
            X509Certificate2 issuerCAKeyCert,
            RSA publicKey,
            X509SignatureGenerator generator,
            bool caCert = false,
            string extensionUrl = null
            )
        {
            if (publicKey == null)
            {
                throw new NotSupportedException("Need a public key and a CA certificate.");
            }

            if (publicKey.KeySize != keySize)
            {
                throw new NotSupportedException(string.Format("Public key size {0} does not match expected key size {1}", publicKey.KeySize, keySize));
            }

            // new serial number
            byte[] serialNumber = new byte[SerialNumberLength];
            RandomNumberGenerator.Fill(serialNumber);
            serialNumber[0] &= 0x7F;

            var request = new CertificateRequest(subjectName, publicKey, GetRSAHashAlgorithmName(hashSizeInBits), RSASignaturePadding.Pkcs1);

            // Basic constraints
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(caCert, caCert, 0, true));

            // Subject Key Identifier
            var ski = new X509SubjectKeyIdentifierExtension(
                request.PublicKey,
                X509SubjectKeyIdentifierHashAlgorithm.Sha1,
                false);
            request.CertificateExtensions.Add(ski);

            // Authority Key Identifier
            if (issuerCAKeyCert != null)
            {
                request.CertificateExtensions.Add(BuildAuthorityKeyIdentifier(issuerCAKeyCert));
            }
            else
            {
                request.CertificateExtensions.Add(BuildAuthorityKeyIdentifier(subjectDN, serialNumber.Reverse().ToArray(), ski));
            }

            if (caCert)
            {
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                        true));

                if (extensionUrl != null)
                {
                    // add CRL endpoint, if available
                    request.CertificateExtensions.Add(
                        BuildX509CRLDistributionPoints(PatchExtensionUrl(extensionUrl, serialNumber))
                        );
                }
            }
            else
            {
                // Key Usage
                X509KeyUsageFlags defaultFlags =
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.DataEncipherment |
                        X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.KeyEncipherment;
                if (issuerCAKeyCert == null)
                {
                    // self signed case
                    defaultFlags |= X509KeyUsageFlags.KeyCertSign;
                }
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(defaultFlags, true));

                // Enhanced key usage
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection {
                        new Oid("1.3.6.1.5.5.7.3.1"),
                        new Oid("1.3.6.1.5.5.7.3.2") }, true));

                // Subject Alternative Name
                //var subjectAltName = BuildSubjectAlternativeName(applicationUri, domainNames);
                //request.CertificateExtensions.Add(new X509Extension(subjectAltName, false));

                if (issuerCAKeyCert != null &&
                    extensionUrl != null)
                {   // add Authority Information Access, if available
                    request.CertificateExtensions.Add(
                        BuildX509AuthorityInformationAccess(new string[] { PatchExtensionUrl(extensionUrl, issuerCAKeyCert.SerialNumber) })
                        );
                }
            }

            if (issuerCAKeyCert != null)
            {
                if (notAfter > issuerCAKeyCert.NotAfter)
                {
                    notAfter = issuerCAKeyCert.NotAfter;
                }
                if (notBefore < issuerCAKeyCert.NotBefore)
                {
                    notBefore = issuerCAKeyCert.NotBefore;
                }
            }

            var issuerSubjectName = issuerCAKeyCert != null ? issuerCAKeyCert.SubjectName : subjectDN;
            X509Certificate2 signedCert = request.Create(
                issuerSubjectName,
                generator,
                notBefore,
                notAfter,
                serialNumber
                );

            return Task.FromResult(signedCert);
        }

        /// <summary>
        /// Get RSA public key from a CSR.
        /// </summary>
        public static RSA GetRSAPublicKey(Org.BouncyCastle.Asn1.X509.SubjectPublicKeyInfo subjectPublicKeyInfo)
        {
            Org.BouncyCastle.Crypto.AsymmetricKeyParameter asymmetricKeyParameter = Org.BouncyCastle.Security.PublicKeyFactory.CreateKey(subjectPublicKeyInfo);
            Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters rsaKeyParameters = (Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters)asymmetricKeyParameter;
            RSAParameters rsaKeyInfo = new RSAParameters
            {
                Modulus = rsaKeyParameters.Modulus.ToByteArrayUnsigned(),
                Exponent = rsaKeyParameters.Exponent.ToByteArrayUnsigned()
            };
            RSA rsa = RSA.Create(rsaKeyInfo);
            return rsa;
        }

        private static string GetRSAHashAlgorithm(uint hashSizeInBits)
        {
            if (hashSizeInBits <= 160)
            {
                return "SHA1WITHRSA";
            }

            if (hashSizeInBits <= 224)
            {
                return "SHA224WITHRSA";
            }
            else if (hashSizeInBits <= 256)
            {
                return "SHA256WITHRSA";
            }
            else if (hashSizeInBits <= 384)
            {
                return "SHA384WITHRSA";
            }
            else
            {
                return "SHA512WITHRSA";
            }
        }

        private static HashAlgorithmName GetRSAHashAlgorithmName(uint hashSizeInBits)
        {
            if (hashSizeInBits <= 160)
            {
                return HashAlgorithmName.SHA1;
            }
            else if (hashSizeInBits <= 256)
            {
                return HashAlgorithmName.SHA256;
            }
            else if (hashSizeInBits <= 384)
            {
                return HashAlgorithmName.SHA384;
            }
            else
            {
                return HashAlgorithmName.SHA512;
            }
        }


        /// <summary>
        /// Read the Crl number from a X509Crl.
        /// </summary>
        private static Org.BouncyCastle.Math.BigInteger GetCrlNumber(Org.BouncyCastle.X509.X509Crl crl)
        {
            Org.BouncyCastle.Math.BigInteger crlNumber = Org.BouncyCastle.Math.BigInteger.One;
            try
            {
                Org.BouncyCastle.Asn1.Asn1Object asn1Object = GetExtensionValue(crl, Org.BouncyCastle.Asn1.X509.X509Extensions.CrlNumber);
                if (asn1Object != null)
                {
                    crlNumber = Org.BouncyCastle.Asn1.DerInteger.GetInstance(asn1Object).PositiveValue;
                }
            }
            finally
            {
            }
            return crlNumber;
        }

        /// <summary>
        /// Get the value of an extension oid.
        /// </summary>
        private static Org.BouncyCastle.Asn1.Asn1Object GetExtensionValue(
            Org.BouncyCastle.X509.IX509Extension extension,
            Org.BouncyCastle.Asn1.DerObjectIdentifier oid)
        {
            Org.BouncyCastle.Asn1.Asn1OctetString asn1Octet = extension.GetExtensionValue(oid);
            if (asn1Octet != null)
            {
                return Org.BouncyCastle.X509.Extension.X509ExtensionUtilities.FromExtensionValue(asn1Octet);
            }
            return null;
        }

        /// <summary>
        /// Get public key parameters from a X509Certificate2
        /// </summary>
        private static Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters GetPublicKeyParameter(X509Certificate2 certificate)
        {
            using (RSA rsa = certificate.GetRSAPublicKey())
            {
                RSAParameters rsaParams = rsa.ExportParameters(false);
                return new Org.BouncyCastle.Crypto.Parameters.RsaKeyParameters(
                    false,
                    new Org.BouncyCastle.Math.BigInteger(1, rsaParams.Modulus),
                    new Org.BouncyCastle.Math.BigInteger(1, rsaParams.Exponent));
            }
        }

        /// <summary>
        /// Get the serial number from a certificate as BigInteger.
        /// </summary>
        private static Org.BouncyCastle.Math.BigInteger GetSerialNumber(X509Certificate2 certificate)
        {
            byte[] serialNumber = certificate.GetSerialNumber();
            Array.Reverse(serialNumber);
            return new Org.BouncyCastle.Math.BigInteger(1, serialNumber);
        }

        /// <summary>
        /// Convert a hex string to a byte array.
        /// </summary>
        /// <param name="hexString">The hex string</param>
        internal static byte[] HexToByteArray(string hexString)
        {
            byte[] bytes = new byte[hexString.Length / 2];

            for (int i = 0; i < hexString.Length; i += 2)
            {
                string s = hexString.Substring(i, 2);
                bytes[i / 2] = byte.Parse(s, System.Globalization.NumberStyles.HexNumber, null);
            }

            return bytes;
        }

        /// <summary>
        /// Build the Authority Key Identifier from an Issuer CA certificate.
        /// </summary>
        /// <param name="issuerCaCertificate">The issuer CA certificate</param>
        private static X509Extension BuildAuthorityKeyIdentifier(X509Certificate2 issuerCaCertificate)
        {
            // force exception if SKI is not present
            var ski = issuerCaCertificate.Extensions.OfType<X509SubjectKeyIdentifierExtension>().Single();
            return BuildAuthorityKeyIdentifier(issuerCaCertificate.SubjectName, issuerCaCertificate.GetSerialNumber(), ski);
        }

        /// <summary>
        /// Build the CRL Distribution Point extension.
        /// </summary>
        /// <param name="distributionPoint">The CRL distribution point</param>
        private static X509Extension BuildX509CRLDistributionPoints(
            string distributionPoint
            )
        {
            var context0 = new Asn1Tag(TagClass.ContextSpecific, 0, true);
            Asn1Tags distributionPointChoice = context0;
            Asn1Tag fullNameChoice = context0;
            Asn1Tag generalNameUriChoice = new Asn1Tag(TagClass.ContextSpecific, 6);

            using (AsnWriter writer = new AsnWriter(AsnEncodingRules.DER))
            {
                writer.PushSequence();
                writer.PushSequence();
                writer.PushSequence(distributionPointChoice);
                writer.PushSequence(fullNameChoice);
                writer.WriteCharacterString(
                    generalNameUriChoice,
                    UniversalTagNumber.IA5String,
                    distributionPoint);
                writer.PopSequence(fullNameChoice);
                writer.PopSequence(distributionPointChoice);
                writer.PopSequence();
                writer.PopSequence();
                return new X509Extension("2.5.29.31", writer.Encode(), false);
            }
        }

        /// <summary>
        /// Build the Authority information Access extension.
        /// </summary>
        /// <param name="caIssuerUrls">Array of CA Issuer Urls</param>
        /// <param name="ocspResponder">optional, the OCSP responder </param>
        private static X509Extension BuildX509AuthorityInformationAccess(
            string[] caIssuerUrls,
            string ocspResponder = null
            )
        {
            if (String.IsNullOrEmpty(ocspResponder) &&
               (caIssuerUrls == null ||
               (caIssuerUrls != null && caIssuerUrls.Length == 0)))
            {
                throw new ArgumentNullException(nameof(caIssuerUrls), "One CA Issuer Url or OCSP responder is required for the extension.");
            }

            var context0 = new Asn1Tag(TagClass.ContextSpecific, 0, true);
            Asn1Tag generalNameUriChoice = new Asn1Tag(TagClass.ContextSpecific, 6);
            using (AsnWriter writer = new AsnWriter(AsnEncodingRules.DER))
            {
                writer.PushSequence();
                if (caIssuerUrls != null)
                {
                    foreach (var caIssuerUrl in caIssuerUrls)
                    {
                        writer.PushSequence();
                        writer.WriteObjectIdentifier("1.3.6.1.5.5.7.48.2");
                        writer.WriteCharacterString(
                            generalNameUriChoice,
                            UniversalTagNumber.IA5String,
                            caIssuerUrl);
                        writer.PopSequence();
                    }
                }
                if (!String.IsNullOrEmpty(ocspResponder))
                {
                    writer.PushSequence();
                    writer.WriteObjectIdentifier("1.3.6.1.5.5.7.48.1");
                    writer.WriteCharacterString(
                        generalNameUriChoice,
                        UniversalTagNumber.IA5String,
                        ocspResponder);
                    writer.PopSequence();
                }
                writer.PopSequence();
                return new X509Extension("1.3.6.1.5.5.7.1.1", writer.Encode(), false);
            }
        }

        /// <summary>
        /// Build the X509 Authority Key extension.
        /// </summary>
        /// <param name="issuerName">The distinguished name of the issuer</param>
        /// <param name="issuerSerialNumber">The serial number of the issuer</param>
        /// <param name="ski">The subject key identifier extension to use</param>
        private static X509Extension BuildAuthorityKeyIdentifier(
            X500DistinguishedName issuerName,
            byte[] issuerSerialNumber,
            X509SubjectKeyIdentifierExtension ski
            )
        {
            using (AsnWriter writer = new AsnWriter(AsnEncodingRules.DER))
            {
                writer.PushSequence();

                if (ski != null)
                {
                    Asn1Tag keyIdTag = new Asn1Tag(TagClass.ContextSpecific, 0);
                    writer.WriteOctetString(keyIdTag, HexToByteArray(ski.SubjectKeyIdentifier));
                }

                Asn1Tag issuerNameTag = new Asn1Tag(TagClass.ContextSpecific, 1);
                writer.PushSequence(issuerNameTag);

                // Add the tag to constructed context-specific 4 (GeneralName.directoryName)
                Asn1Tag directoryNameTag = new Asn1Tag(TagClass.ContextSpecific, 4, true);
                writer.PushSetOf(directoryNameTag);
                byte[] issuerNameRaw = issuerName.RawData;
                writer.WriteEncodedValue(issuerNameRaw);
                writer.PopSetOf(directoryNameTag);
                writer.PopSequence(issuerNameTag);

                Asn1Tag issuerSerialTag = new Asn1Tag(TagClass.ContextSpecific, 2);
                System.Numerics.BigInteger issuerSerial = new System.Numerics.BigInteger(issuerSerialNumber);
                writer.WriteInteger(issuerSerialTag, issuerSerial);

                writer.PopSequence();
                return new X509Extension("2.5.29.35", writer.Encode(), false);
            }
        }

        /// <summary>
        /// Patch serial number in a Url. byte version.
        /// </summary>
        private static string PatchExtensionUrl(string extensionUrl, byte[] serialNumber)
        {
            string serial = BitConverter.ToString(serialNumber).Replace("-", "");
            return PatchExtensionUrl(extensionUrl, serial);
        }

        /// <summary>
        /// Patch serial number in a Url. string version.
        /// </summary>
        private static string PatchExtensionUrl(string extensionUrl, string serial)
        {
            return extensionUrl.Replace("%serial%", serial.ToLower());
        }

    }
    /// <summary>
    /// The X509 signature generator to sign a digest with a KeyVault key.
    /// </summary>
    public class KeyVaultSignatureGenerator : X509SignatureGenerator
    {
        private X509Certificate2 _issuerCert;
        private KeyVaultServiceClient _keyVaultServiceClient;
        private readonly string _signingKey;

        /// <summary>
        /// Create the KeyVault signature generator.
        /// </summary>
        /// <param name="keyVaultServiceClient">The KeyVault service client to use</param>
        /// <param name="signingKey">The KeyVault signing key</param>
        /// <param name="issuerCertificate">The issuer certificate used for signing</param>
        public KeyVaultSignatureGenerator(
            KeyVaultServiceClient keyVaultServiceClient,
            string signingKey,
            X509Certificate2 issuerCertificate)
        {
            _issuerCert = issuerCertificate;
            _keyVaultServiceClient = keyVaultServiceClient;
            _signingKey = signingKey;
        }

        /// <summary>
        /// The signature factory for Bouncy Castle to sign a digest with a KeyVault key.
        /// </summary>
        public class KeyVaultSignatureFactory : Org.BouncyCastle.Crypto.ISignatureFactory
        {
            private readonly Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier _algID;
            private readonly HashAlgorithmName _hashAlgorithm;
            private readonly X509SignatureGenerator _generator;

            /// <summary>
            /// Constructor which also specifies a source of randomness to be used if one is required.
            /// </summary>
            /// <param name="hashAlgorithm">The name of the signature algorithm to use.</param>
            /// <param name="generator">The signature generator.</param>
            public KeyVaultSignatureFactory(HashAlgorithmName hashAlgorithm, X509SignatureGenerator generator)
            {
                DerObjectIdentifier sigOid;
                if (hashAlgorithm == HashAlgorithmName.SHA256)
                {
                    sigOid = Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers.Sha256WithRsaEncryption;
                }
                else if (hashAlgorithm == HashAlgorithmName.SHA384)
                {
                    sigOid = Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers.Sha384WithRsaEncryption;
                }
                else if (hashAlgorithm == HashAlgorithmName.SHA512)
                {
                    sigOid = Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers.Sha512WithRsaEncryption;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(hashAlgorithm));
                }
                _hashAlgorithm = hashAlgorithm;
                _generator = generator;
                _algID = new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(sigOid);
            }

            /// <inheritdoc/>
            public object AlgorithmDetails => _algID;

            /// <inheritdoc/>
            public Org.BouncyCastle.Crypto.IStreamCalculator CreateCalculator()
            {
                return new KeyVaultStreamCalculator(_generator, _hashAlgorithm);
            }
        }

        /// <summary>
        /// Signs a Bouncy Castle digest stream with the .Net X509SignatureGenerator.
        /// </summary>
        public class KeyVaultStreamCalculator : Org.BouncyCastle.Crypto.IStreamCalculator
        {
            private X509SignatureGenerator _generator;
            private readonly HashAlgorithmName _hashAlgorithm;

            /// <summary>
            /// Ctor for the stream calculator. 
            /// </summary>
            /// <param name="generator">The X509SignatureGenerator to sign the digest.</param>
            /// <param name="hashAlgorithm">The hash algorithm to use for the signature.</param>
            public KeyVaultStreamCalculator(
                X509SignatureGenerator generator,
                HashAlgorithmName hashAlgorithm)
            {
                Stream = new MemoryStream();
                _generator = generator;
                _hashAlgorithm = hashAlgorithm;
            }

            /// <summary>
            /// The digest stream (MemoryStream).
            /// </summary>
            public Stream Stream { get; }

            /// <summary>
            /// Callback signs the digest with X509SignatureGenerator.
            /// </summary>
            public object GetResult()
            {
                var memStream = Stream as MemoryStream;
                var digest = memStream.ToArray();
                var signature = _generator.SignData(digest, _hashAlgorithm);
                return new MemoryBlockResult(signature);
            }
        }

        /// <summary>
        /// Helper for Bouncy Castle signing operation to store the result in a memory block.
        /// </summary>
        public class MemoryBlockResult : Org.BouncyCastle.Crypto.IBlockResult
        {
            private readonly byte[] _data;
            /// <inheritdoc/>
            public MemoryBlockResult(byte[] data)
            {
                _data = data;
            }
            /// <inheritdoc/>
            public byte[] Collect()
            {
                return _data;
            }
            /// <inheritdoc/>
            public int Collect(byte[] destination, int offset)
            {
                throw new NotImplementedException();
            }
        }
    }
}
