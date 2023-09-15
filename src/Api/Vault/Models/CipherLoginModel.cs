﻿using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherLoginModel
{
    public CipherLoginModel() { }

    public CipherLoginModel(CipherLoginData data)
    {
        Uris = data.Uris?.Select(u => new CipherLoginUriModel(u))?.ToList();
        if (!Uris?.Any() ?? true)
        {
            Uri = data.Uri;
        }

        if (data.Fido2Keys != null)
        {
            Fido2Keys = data.Fido2Keys.Select(key => new CipherFido2KeyModel(key)).ToArray();
        }

        Username = data.Username;
        Password = data.Password;
        PasswordRevisionDate = data.PasswordRevisionDate;
        Totp = data.Totp;
        AutofillOnPageLoad = data.AutofillOnPageLoad;
    }

    [EncryptedString]
    [EncryptedStringLength(10000)]
    public string Uri
    {
        get => Uris?.FirstOrDefault()?.Uri;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (Uris == null)
            {
                Uris = new List<CipherLoginUriModel>();
            }

            Uris.Add(new CipherLoginUriModel(value));
        }
    }
    public List<CipherLoginUriModel> Uris { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Username { get; set; }
    [EncryptedString]
    [EncryptedStringLength(5000)]
    public string Password { get; set; }
    public DateTime? PasswordRevisionDate { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Totp { get; set; }
    public bool? AutofillOnPageLoad { get; set; }
    public CipherFido2KeyModel[] Fido2Keys { get; set; }

    public class CipherLoginUriModel
    {
        public CipherLoginUriModel() { }

        public CipherLoginUriModel(string uri)
        {
            Uri = uri;
        }

        public CipherLoginUriModel(CipherLoginData.CipherLoginUriData uri)
        {
            Uri = uri.Uri;
            Match = uri.Match;
        }

        [EncryptedString]
        [EncryptedStringLength(10000)]
        public string Uri { get; set; }
        public UriMatchType? Match { get; set; } = null;

        public CipherLoginData.CipherLoginUriData ToCipherLoginUriData()
        {
            return new CipherLoginData.CipherLoginUriData { Uri = Uri, Match = Match, };
        }
    }
}
