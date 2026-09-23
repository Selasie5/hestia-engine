# Deploy Hestia to Render

Hestia deploys as two Docker web services: `hestia-web` (Blazor Server) and `hestia-api` (ASP.NET Core API). The checked-in `render.yaml` wires the web service to the API over Render's private network and gives the SQLite database a persistent disk.

## Before you deploy

1. Push the repository to GitHub or GitLab.
2. Create Paystack test keys. Keep them out of Git.
3. Choose a paid plan for the API. SQLite requires the persistent disk declared in the Blueprint; Render disks are not available on free web services.

For a higher-availability production system, migrate from SQLite to a managed database before scaling. A Render disk is attached to one service instance, so the API must remain at one instance while SQLite is in use.

## Create the Blueprint

1. In the Render dashboard, select **New > Blueprint**.
2. Connect this repository. Render detects `render.yaml` in the repository root.
3. Review the two services and apply the Blueprint.
4. When prompted, enter `Paystack__SecretKey` and `Paystack__PublicKey`. Use test keys until the complete payment flow has been verified.
5. Wait for `hestia-api` to pass `/health`, then for `hestia-web` to pass `/`.

The Blueprint generates `Jwt__Secret`. Do not replace it after users begin signing in because changing it invalidates issued access tokens. Both EF Core schemas are migrated automatically when the API starts; development-only sample records are not seeded in Production.

The Blueprint also declares `PORT=8080`, matching the .NET container's default HTTP port, so Render does not need a discovery restart. `ReverseProxy__TerminatesTls=true` prevents the applications from trying to redirect Render's internal HTTP traffic to a nonexistent container HTTPS port. Render still provides HTTPS publicly at its load balancer.

## Configure Paystack

After Render assigns the API URL, configure the Paystack webhook URL as:

```text
https://<your-hestia-api-host>/api/v1.0/Payments/webhook
```

Use the public API hostname for the webhook. The Blazor application itself calls the API through Render's private `host:port`, supplied as `ApiHostPort`; this keeps internal application traffic off the public internet.

## Optional email settings

Add these environment variables to `hestia-api` in the Render dashboard if SMTP delivery is required:

```text
Smtp__Host
Smtp__Port
Smtp__User
Smtp__Password
Smtp__From
```

Without SMTP configuration, the application retains its configured development/fallback behavior.

## Verify the deployment

1. Open `https://<your-hestia-api-host>/health` and confirm a successful response.
2. Open the web service URL and register a student account.
3. Sign in and confirm hostel and room browsing.
4. Submit an application, approve it from an admin account, and confirm that an allocation and payment are created.
5. Initiate a Paystack test payment. Confirm that checkout opens, the protected QR renders, PNG/SVG downloads work, and verification updates the payment.
6. Restart the API service and confirm existing records remain available. This verifies that SQLite is writing to `/data/hostelsystem.db` on the persistent disk.

The API's ASP.NET Core data-protection keys are stored under `/data/data-protection-keys`, so Identity token protection remains stable across API restarts. Render may still report that these XML keys are not application-level encrypted; the persistent disk is encrypted at rest, and a certificate-backed key encryptor can be added if your threat model requires separate key encryption.

## Troubleshooting

- **Web cannot reach API:** confirm `ApiHostPort` is populated from `hestia-api` and both services are in the same Render workspace/region.
- **Data disappears after deploy:** confirm the API disk is mounted at `/data` and `ConnectionStrings__DefaultConnection` is exactly `Data Source=/data/hostelsystem.db`.
- **API deploy is unhealthy:** inspect API logs, then verify `/health`, `Jwt__Secret`, and the database connection string.
- **Render detects a new port and restarts once:** set `PORT=8080` on each manually created service. The checked-in Blueprint already does this.
- **`Failed to determine the https port for redirect`:** set `ReverseProxy__TerminatesTls=true`. Render terminates public HTTPS before forwarding the request to the container.
- **`HEAD /` returns 404:** redeploy the current API revision; it exposes a lightweight root status endpoint as well as `/health`.
- **Paystack returns configuration errors:** confirm the two Paystack environment variables and webhook URL, then redeploy the API.
- **First page load is slow:** the free web service can spin down when idle. Move `hestia-web` to a paid plan to avoid cold starts.

## Manual alternative

If you do not use the Blueprint, create two Docker web services from the same repository. Use `Dockerfile` for the API and `Dockerfile.web` for the web app. Configure the variables and API `/data` disk shown in `render.yaml`, including `PORT=8080` and `ReverseProxy__TerminatesTls=true`, then set the web service's `ApiHostPort` to the API service's private hostname and port.
