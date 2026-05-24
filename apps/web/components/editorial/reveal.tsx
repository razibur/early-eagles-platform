"use client"

import { motion, useReducedMotion, type Variants } from "motion/react"

const containerVariants: Variants = {
  hidden: {},
  visible: {
    transition: {
      staggerChildren: 0.09,
      delayChildren: 0.05,
    },
  },
}

const itemVariants: Variants = {
  hidden: { opacity: 0, y: 18, filter: "blur(6px)" },
  visible: {
    opacity: 1,
    y: 0,
    filter: "blur(0px)",
    transition: { duration: 0.7, ease: [0.22, 1, 0.36, 1] },
  },
}

export function RevealStack({
  children,
  className,
}: {
  children: React.ReactNode
  className?: string
}) {
  const reduceMotion = useReducedMotion()
  return (
    <motion.div
      className={className}
      initial={reduceMotion ? "visible" : "hidden"}
      animate="visible"
      variants={containerVariants}
    >
      {children}
    </motion.div>
  )
}

export function RevealItem({
  children,
  className,
  as = "div",
}: {
  children: React.ReactNode
  className?: string
  as?: "div" | "section" | "article" | "header"
}) {
  const Comp = motion[as]
  const reduceMotion = useReducedMotion()
  return (
    <Comp
      className={className}
      variants={itemVariants}
      initial={reduceMotion ? "visible" : undefined}
    >
      {children}
    </Comp>
  )
}
