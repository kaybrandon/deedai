import { NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../auth";
import { OCR_RIBBON_STEPS, type OcrRibbonStep } from "../theme";

const STEP_HREF: Record<OcrRibbonStep, string> = {
  Upload: "/upload",
  Queued: "/documents?status=Queued",
  Processing: "/documents?status=Processing",
  Review: "/documents?status=NeedsReview",
  Ready: "/documents?status=Ready"
};

export default function OcrRibbon({ current }: { current?: OcrRibbonStep | null }) {
  const { canUpload } = useAuth();
  const navigate = useNavigate();

  return (
    <nav className="ocr-ribbon" aria-label="OCR pipeline">
      {OCR_RIBBON_STEPS.map((step, index) => {
        const href = STEP_HREF[step];
        const isCurrent = current === step;
        const className = `ocr-step${isCurrent ? " is-current" : ""}`;
        return (
          <span key={step} className="ocr-step-wrap">
            {index > 0 && (
              <span className="ocr-arrow" aria-hidden="true">
                →
              </span>
            )}
            {step === "Upload" && !canUpload ? (
              <button
                className={className}
                type="button"
                aria-current={isCurrent ? "step" : undefined}
                onClick={() => navigate("/denied", { state: { action: "upload documents" } })}
              >
                <span className="ocr-dot" aria-hidden="true" />
                {step}
              </button>
            ) : (
              <NavLink className={className} to={href} aria-current={isCurrent ? "step" : undefined}>
                <span className="ocr-dot" aria-hidden="true" />
                {step}
              </NavLink>
            )}
          </span>
        );
      })}
    </nav>
  );
}
