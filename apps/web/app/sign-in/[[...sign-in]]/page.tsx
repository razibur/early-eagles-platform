import type { Metadata } from "next"
import { notFound } from "next/navigation"
import { SignIn } from "@clerk/nextjs"

import { AuthStage } from "@/components/auth/auth-stage"
import { authMode } from "@/lib/auth/config"
import { SIGN_IN_PATH, SIGN_UP_PATH } from "@/lib/auth/routes"

export const metadata: Metadata = { title: "Sign in — Early Eagles" }

// Clerk's sign-in (P0-039.3). An optional catch-all: Clerk moves through its
// own steps (/sign-in/factor-one, /sign-in/sso-callback …) under this path.
export default function SignInPage() {
  // A build without Clerk (EE_AUTH_DISABLED=1) has no provider to sign in with.
  if (authMode() !== "clerk") notFound()

  return (
    <AuthStage
      eyebrow="Members · Sign in"
      title={
        <>
          Back on
          <br />
          <span className="text-primary">court.</span>
        </>
      }
      lede="Sign in to see this Sunday's roster, your dues and what the club's been saying."
    >
      <SignIn path={SIGN_IN_PATH} signUpUrl={SIGN_UP_PATH} />
    </AuthStage>
  )
}
