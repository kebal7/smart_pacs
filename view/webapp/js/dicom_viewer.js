// 1. Initial Setup
cornerstoneWADOImageLoader.external.cornerstone = cornerstone;
cornerstoneWADOImageLoader.external.dicomParser = dicomParser;

const element = document.getElementById('viewer-container');
cornerstone.enable(element);

let isDragging = false;
let lastMousePos = { x: 0, y: 0 };
let activeMode = 'none'; // 'zoom', 'pan', or 'contrast'

// 3. Load the Image
const imageId = 'wadouri:https://raw.githubusercontent.com/cornerstonejs/cornerstoneWADOImageLoader/master/testImages/CTImage.dcm';

cornerstone.loadImage(imageId).then(image => {
    cornerstone.displayImage(element, image);
    
    // --- CUSTOM TOOL LOGIC STARTS HERE ---

    element.addEventListener('mousedown', (e) => {
        isDragging = true;
        lastMousePos = { x: e.clientX, y: e.clientY };
        
        // Decide mode based on button: 0=Left(Contrast), 1=Middle(Pan), 2=Right(Zoom)
        if (e.button === 0) activeMode = 'contrast';
        if (e.button === 1) activeMode = 'pan';
        if (e.button === 2) activeMode = 'zoom';
    });

    window.addEventListener('mousemove', (e) => {
        if (!isDragging) return;

        // Calculate how much the mouse moved
        const deltaX = e.clientX - lastMousePos.x;
        const deltaY = e.clientY - lastMousePos.y;
        lastMousePos = { x: e.clientX, y: e.clientY };

        // Get the current state of the image
        const viewport = cornerstone.getViewport(element);

        if (activeMode === 'zoom') {
            // Change scale (1.0 is 100%)
            viewport.scale += (deltaY * -0.01); 
            if (viewport.scale < 0.1) viewport.scale = 0.1; // Prevent inversion
        } 
        else if (activeMode === 'pan') {
            // Change X/Y translation
            viewport.translation.x += (deltaX / viewport.scale);
            viewport.translation.y += (deltaY / viewport.scale);
        }
        else if (activeMode === 'contrast') {
            // VOI (Value of Interest) / Windowing
            viewport.voi.windowWidth += deltaX * 2;
            viewport.voi.windowCenter += deltaY * 2;
        }

        // Apply the changes back to the viewer
        cornerstone.setViewport(element, viewport);
    });

    window.addEventListener('mouseup', () => {
        isDragging = false;
        activeMode = 'none';
    });

    // Handle Mouse Wheel Zoom
    element.addEventListener('wheel', (e) => {
        e.preventDefault();
        const viewport = cornerstone.getViewport(element);
        const zoomStep = e.deltaY > 0 ? 0.9 : 1.1;
        viewport.scale *= zoomStep;
        cornerstone.setViewport(element, viewport);
    }, { passive: false });

});