#!/usr/bin/env python3
"""Stripe-powered leasing flow for the MES indicator bundle."""
from __future__ import annotations

import os
from pathlib import Path
from typing import Optional

from dotenv import load_dotenv
from flask import (
    Flask,
    abort,
    jsonify,
    redirect,
    render_template,
    request,
    send_file,
    url_for,
)
import stripe

# Load environment variables from a .env file when running locally
load_dotenv()

app = Flask(__name__, static_folder="static")

# Configuration via environment variables
STRIPE_API_KEY: Optional[str] = os.getenv("STRIPE_API_KEY") or os.getenv("STRIPE_SECRET_KEY")
STRIPE_PRICE_ID: Optional[str] = os.getenv("STRIPE_PRICE_ID")
SUCCESS_URL_OVERRIDE: Optional[str] = os.getenv("STRIPE_SUCCESS_URL")
CANCEL_URL_OVERRIDE: Optional[str] = os.getenv("STRIPE_CANCEL_URL")

STRIPE_READY = bool(STRIPE_API_KEY and STRIPE_PRICE_ID)

if STRIPE_READY:
    stripe.api_key = STRIPE_API_KEY

BUNDLE_PATH = Path(__file__).with_name("combined_indicator_bundle.zip")
if not BUNDLE_PATH.exists():
    raise RuntimeError("combined_indicator_bundle.zip is missing. Generate it before starting the server.")


def _success_url() -> str:
    """Determine the success URL for Stripe checkout."""
    if SUCCESS_URL_OVERRIDE:
        if "{CHECKOUT_SESSION_ID}" not in SUCCESS_URL_OVERRIDE:
            raise RuntimeError("STRIPE_SUCCESS_URL must contain '{CHECKOUT_SESSION_ID}' placeholder.")
        return SUCCESS_URL_OVERRIDE
    return url_for("success", _external=True) + "?session_id={CHECKOUT_SESSION_ID}"


def _cancel_url() -> str:
    """Determine the cancel URL for Stripe checkout."""
    if CANCEL_URL_OVERRIDE:
        return CANCEL_URL_OVERRIDE
    return url_for("cancel", _external=True)


def _checkout_config() -> tuple[str, list[dict]]:
    """Detect whether the price is one-time or recurring and return mode+items."""
    price = stripe.Price.retrieve(STRIPE_PRICE_ID)
    is_recurring = bool(price.get("recurring"))
    mode = "subscription" if is_recurring else "payment"
    line_items = [{"price": STRIPE_PRICE_ID, "quantity": 1}]
    return mode, line_items


@app.route("/", methods=["GET"])
def index() -> str:
    """Landing page with a classic button for older clients."""
    return render_template("index.html", stripe_ready=STRIPE_READY)


@app.route("/create-checkout-session", methods=["POST"])
def create_checkout_session():
    """Create a Checkout Session and redirect the client to Stripe."""
    if not STRIPE_READY:
        return jsonify({"error": "Stripe is not configured. Set STRIPE_API_KEY (or STRIPE_SECRET_KEY) and STRIPE_PRICE_ID."}), 503
    if not STRIPE_PRICE_ID:
        return jsonify({"error": "STRIPE_PRICE_ID is missing."}), 503

    try:
        mode, line_items = _checkout_config()
    except Exception as exc:
        return jsonify({"error": f"Unable to load Stripe price: {exc}"}), 400

    try:
        session = stripe.checkout.Session.create(
            mode=mode,
            payment_method_types=["card"],
            line_items=line_items,
            success_url=_success_url(),
            cancel_url=_cancel_url(),
            allow_promotion_codes=True,
        )
    except Exception as exc:
        return jsonify({"error": str(exc)}), 400

    return redirect(session.url, code=303)


@app.route("/success", methods=["GET"])
def success() -> str:
    """Success page with download instructions."""
    if not STRIPE_READY:
        abort(503, "Stripe is not configured on this server")

    session_id = request.args.get("session_id")
    if not session_id:
        abort(400, "Missing session_id query parameter")

    try:
        session = stripe.checkout.Session.retrieve(session_id)
    except Exception:
        abort(400, "Unable to validate Stripe session")

    if session.get("payment_status") != "paid":
        abort(403, "Payment has not been completed yet")

    customer_email = session.get("customer_details", {}).get("email")
    return render_template("success.html", session_id=session_id, customer_email=customer_email)


@app.route("/cancel", methods=["GET"])
def cancel() -> str:
    """Simple cancel page."""
    return render_template("cancel.html")


@app.route("/download/<session_id>", methods=["GET"])
def download(session_id: str):
    """Allow downloads only for paid sessions."""
    if not STRIPE_READY:
        abort(503, "Stripe is not configured on this server")

    try:
        session = stripe.checkout.Session.retrieve(session_id)
    except Exception:
        abort(400, "Unable to validate Stripe session")

    if session.get("payment_status") != "paid":
        abort(403, "Payment has not been completed yet")

    return send_file(BUNDLE_PATH, as_attachment=True, download_name="MES_Indicators.zip")


if __name__ == "__main__":
    # Use Flask's built-in server for local testing. Enable debug by setting FLASK_DEBUG=1.
    debug_enabled = os.getenv("FLASK_DEBUG", "0") == "1"
    app.run(host="0.0.0.0", port=int(os.getenv("PORT", "8080")), debug=debug_enabled)
