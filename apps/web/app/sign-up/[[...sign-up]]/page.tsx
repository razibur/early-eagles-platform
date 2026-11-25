import type { Metadata } from "next"
import { notFound } from "next/navigation"
import { SignUp } from "@clerk/nextjs"

import { AuthStage } from "@/components/auth/auth-stage"
import { authMode } from "@/lib/auth/config"
import { SIGN_IN_PATH, SIGN_UP_PATH } from "@/lib/auth/routes"

export const metadata: Metadata = { title: "Join — Early Eagles" }

// Clerk's sign-up (P0-039.3). Making an account doesn't put anyone in a club:
// membership comes from the club's admin (a Clerk organization invitation).
export default function SignUpPage() {
  if (authMode() !== "clerk") notFound()

  return (
    <AuthStage
      eyebrow="New player · Join"
      title={
        <>
          Grab a
          <br />
          <span className="text-primary">racket.</span>
        </>
      }
      lede="Make your account here. Your club's admin then adds you to the roster, and Sunday shows up on your phone."
    >
      <SignUp path={SIGN_UP_PATH} signInUrl={SIGN_IN_PATH} />
    </AuthStage>
  )
}
