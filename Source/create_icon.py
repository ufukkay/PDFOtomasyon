from PIL import Image, ImageDraw

def create_icon(path):
    size = (256, 256)
    # Create image with transparent background
    img = Image.new('RGBA', size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Draw rounded background (Blue #2563EB)
    # Circle/Rounded Rect
    bg_color = (37, 99, 235, 255) # #2563EB
    margin = 20
    draw.ellipse([margin, margin, size[0]-margin, size[1]-margin], fill=bg_color)

    # Draw Document Icon (White)
    doc_w = 100
    doc_h = 140
    doc_x = (size[0] - doc_w) // 2
    doc_y = (size[1] - doc_h) // 2
    
    # Document main body
    doc_color = (255, 255, 255, 255)
    
    # Folded corner effect (top right)
    fold_size = 30
    
    # Points for document shape with fold
    points = [
        (doc_x, doc_y), # Top-Left
        (doc_x + doc_w - fold_size, doc_y), # Top-Right (start of fold)
        (doc_x + doc_w, doc_y + fold_size), # Top-Right (end of fold)
        (doc_x + doc_w, doc_y + doc_h), # Bottom-Right
        (doc_x, doc_y + doc_h) # Bottom-Left
    ]
    
    draw.polygon(points, fill=doc_color)
    
    # Draw the fold triangle (slightly darker white/gray)
    fold_color = (220, 220, 220, 255)
    fold_points = [
        (doc_x + doc_w - fold_size, doc_y),
        (doc_x + doc_w - fold_size, doc_y + fold_size),
        (doc_x + doc_w, doc_y + fold_size)
    ]
    draw.polygon(fold_points, fill=fold_color)

    # Draw "PRO" text or lines simulating text
    line_h = 10
    line_gap = 18
    start_y = doc_y + 40
    pad_x = 20
    
    # 3 lines of text
    for i in range(3):
        y = start_y + (i * line_gap)
        draw.rectangle(
            [doc_x + pad_x, y, doc_x + doc_w - pad_x, y + line_h], 
            fill=(37, 99, 235, 200) # Blue text lines
        )

    # Save as ICO (containing multiple sizes)
    img.save(path, format='ICO', sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
    print(f"Icon created at: {path}")

if __name__ == "__main__":
    create_icon(r"c:\Users\ufuk.kaya\Desktop\New folder (3)\PDFOtomasyon\Source\PDFAutomation\icon.ico")
    # Also save to Installer project for safety
    create_icon(r"c:\Users\ufuk.kaya\Desktop\New folder (3)\PDFOtomasyon\Source\PDFAutomation.Installer\icon.ico")
