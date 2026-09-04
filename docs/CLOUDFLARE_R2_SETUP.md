# Cloudflare R2 setup

Phase 8 is implemented, but production uploads require these one-time Cloudflare actions.

1. Create an R2 bucket named `bizden-media`. Keep it private.
2. Create an R2 API token with **Object Read & Write** scoped only to this bucket.
3. Copy the S3 endpoint: `https://<account-id>.r2.cloudflarestorage.com`.
4. Add `R2__Endpoint`, `R2__Bucket`, `R2__AccessKeyId`, and `R2__SecretAccessKey` to the API secret store or Docker environment. Never commit them.
5. Add a bucket CORS rule allowing your production web origin and `PUT`, `HEAD` methods, with `Content-Type` allowed.
6. Test a real QR upload on iPhone Safari and Android Chrome.

The browser receives a 10-minute signed PUT URL. The bucket stays private; the API checks object size and content type through an R2 HEAD request before marking a photo complete.
