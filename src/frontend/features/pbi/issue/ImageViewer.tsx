import { t } from "@/shared/i18n";
import { Button, Dialog } from "@/shared/ui/kit";
import type { Picture } from "./VendorMarkdown";

/** An image of an issue or a change at the size it was uploaded, as wide as the window allows. */
export function ImageViewer({ picture, onClose }: { picture: Picture; onClose: () => void }) {
  return (
    <Dialog
      title={picture.alt || t("image.unnamed")}
      onClose={onClose}
      width={Math.min(1100, window.innerWidth - 32)}
      footer={
        <>
          <a href={picture.src} target="_blank" rel="noreferrer" style={{ fontSize: "var(--text-xs)" }}>
            {t("image.open")}
          </a>
          <div style={{ flex: 1 }} />
          <Button variant="outline" size="sm" onClick={onClose}>
            {t("dialog.close")}
          </Button>
        </>
      }
    >
      <img src={picture.src} alt={picture.alt} style={{ maxWidth: "100%", display: "block", margin: "0 auto" }} />
    </Dialog>
  );
}
