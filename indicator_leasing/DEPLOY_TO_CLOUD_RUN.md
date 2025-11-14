# Deploy the indicator leasing app to Cloud Run

> **Prerequisites**
>
> * gcloud CLI installed (Cloud Shell already has it)
> * Stripe secret key and price ID
> * Google Cloud project ID (for example: `my-stripe-leasing`)
>
> Clone or upload this repository so the files are available on Cloud Shell.

---

## 1. Set project ID

```bash
gcloud config set project YOUR_PROJECT_ID
```

Replace `YOUR_PROJECT_ID` with the real Google Cloud project ID (not the project name). Example:

```bash
gcloud config set project my-stripe-leasing
```

---

## 2. Build and push image

From the repository root `/workspaces/MES` run:

```bash
gcloud builds submit --tag gcr.io/YOUR_PROJECT_ID/indicator-leasing .
```

Example:

```bash
gcloud builds submit --tag gcr.io/my-stripe-leasing/indicator-leasing .
```

---

## 3. Create or update secrets

If you already have secrets in Secret Manager, you can add new versions. If not, create them:

```bash
printf 'sk_live_YOUR_STRIPE_SECRET' | gcloud secrets create STRIPE_SECRET_KEY --data-file=-
printf 'price_YOUR_STRIPE_PRICE_ID' | gcloud secrets create STRIPE_PRICE_ID --data-file=-
```

If the secrets already exist, add new versions instead:

```bash
printf 'sk_live_YOUR_STRIPE_SECRET' | gcloud secrets versions add STRIPE_SECRET_KEY --data-file=-
printf 'price_YOUR_STRIPE_PRICE_ID' | gcloud secrets versions add STRIPE_PRICE_ID --data-file=-
```

---

## 4. Deploy to Cloud Run

```bash
gcloud run deploy indicator-leasing \
  --image gcr.io/YOUR_PROJECT_ID/indicator-leasing \
  --region us-east4 \
  --allow-unauthenticated \
  --set-secrets STRIPE_SECRET_KEY=STRIPE_SECRET_KEY:latest \
                 STRIPE_PRICE_ID=STRIPE_PRICE_ID:latest \
  --set-env-vars STRIPE_SUCCESS_URL=https://natureswaysoil.com/indicators/success?session_id={CHECKOUT_SESSION_ID},\
                 STRIPE_CANCEL_URL=https://natureswaysoil.com/indicators/cancel
```

Replace `YOUR_PROJECT_ID`, `STRIPE_SUCCESS_URL`, and `STRIPE_CANCEL_URL` with your real values.

---

## 5. Confirm deployment

The command prints a service URL, for example:

```
Service [indicator-leasing] revision [indicator-leasing-00001-...?] has been deployed and is serving 100 percent of traffic.
URL: https://indicator-leasing-xxxxxx-uc.a.run.app
```

Open the URL in the browser, run through a Stripe test checkout (use a test card like `4242 4242 4242 4242`, any future expiry, any CVC), confirm the download works, then update your website to point to that Cloud Run URL.

---

## Optional: Local test with Gunicorn

```bash
STRIPE_SECRET_KEY=sk_test_... STRIPE_PRICE_ID=price_test_... \
gunicorn -b 0.0.0.0:8080 app:app
```
