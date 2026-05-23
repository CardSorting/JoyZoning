"use client";

import type { PetState } from "@/lib/pet";

export function SickBanner({
  pet,
  onStackTrace,
  onRetry,
}: {
  pet: PetState;
  onStackTrace: () => void;
  onRetry: () => void;
}) {
  if (pet.mood !== "sick" && pet.mood !== "panicking") return null;

  return (
    <div className="pet-panel border-pet-rose/30 p-4" role="alert">
      <p className="text-sm font-semibold text-pet-rose">
        {pet.mood === "panicking"
          ? "Your pet is panicking — execution keeps failing."
          : "Your pet feels sick — something threw."}
      </p>
      <p className="mt-2 text-sm text-pet-muted">
        {pet.friendlyError ?? "Check the observatory basement for raw details."}
      </p>
      <div className="mt-3 flex flex-wrap gap-2">
        <button
          type="button"
          onClick={onStackTrace}
          className="rounded-pet bg-pet-rose/25 px-4 py-2 text-sm font-semibold text-pet-rose"
        >
          Open Stack Trace
        </button>
        {pet.mood === "panicking" && (
          <button
            type="button"
            onClick={onRetry}
            className="rounded-pet pet-card px-4 py-2 text-sm text-pet-muted hover:text-pet-cream"
          >
            Retry
          </button>
        )}
      </div>
    </div>
  );
}
