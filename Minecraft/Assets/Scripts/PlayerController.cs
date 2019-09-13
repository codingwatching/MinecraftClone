﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    public CharacterController characterController;
    public MonitorController monitorController;
    public ChunkManager chunkManager;
    public Transform body;
    public Transform feet;
    public AudioSource stepAudioSource;
    public AudioClip teleportClip;
    public AudioClip hitClip;
    public GameObject canvas;
    [Range(0.0f, 100f)] public float baseSpeed = 10f;
    [Range(0.0f, 100f)] public float jumpPower = 3f;
    [Range(0.0f, 100f)] public float enderPearlThrowingPower = 10f;
    [Range(0.0f, 1.0f)] public float sidewaysSpeedModifier = 0.5f;
    [Range(0.0f, 2.0f)] public float waterSpeedModifier = 0.5f;
    public GameObject splashEffect;
    public GameObject explosionEffect;
    public GameObject breakEffect;
    public GameObject teleportEffect;

    public GameObject tntPrefab;
    public GameObject enderPearlPrefab;

    public Color waterTintColor = Color.blue;
    private Color initialSkyColor;
    public Material[] materialsToColor;

    public new Transform camera;

    private bool _shouldTeleport = false;
    private Vector3 _teleportDestination;
    private float _speed;

    private Vector3Int _prevPosition;
    private Vector3Int _currPosition;
    private BlockType _prevType;

    private Text[] _textComponents;

    private bool _isJumping = false;
    private float _jumpTimer = 0.0f;
    private float _jumpYVelocity = 0.0f;
    [SerializeField] private float _jumpDuration = 2.0f;

    [SerializeField] private bool _isInWater = false;
    //[SerializeField] private bool _cameraIsInWater = false;

    // Mouse
    public float mouseSensitivity = 100.0f;
    public float clampAngle = 80.0f;

    private float rotY = 0.0f; // rotation around the up/y axis
    private float rotX = 0.0f; // rotation around the right/x axis

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        _textComponents = this.canvas.GetComponentsInChildren<Text>();

        initialSkyColor = RenderSettings.fogColor;

        _speed = baseSpeed;

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        // Mouse Movement
        Vector3 rot = camera.localRotation.eulerAngles;
        rotY = rot.y;
        rotX = rot.x;

        PlacePlayerOnSurface(false);
    }
    public void BeginTeleport(Vector3 position) {
        _shouldTeleport = true;
        _teleportDestination = position;
        AudioSource.PlayClipAtPoint(hitClip, this.transform.position);
    }

    private void Teleport() {
        if (RaycastToBlock(100, true, out Vector3Int hitBlockPosition)) {
            Teleport(hitBlockPosition);
            Debug.Log("Teleporting...");
        }
        else {
            Debug.Log("No block to teleport to :(");
        }

    }
    private void Teleport(Vector3 position) {
        characterController.enabled = false;
        Vector3 destination = position + (transform.position - feet.position);
        Debug.Log($"Teleporting {(transform.position - destination).magnitude} units.");
        this.transform.position = position + (transform.position - feet.position);
        GameObject effect = Instantiate(teleportEffect, this.camera.position, Quaternion.identity);        
    }

    void PlacePlayerOnSurface(bool playAudio)
    {
        Vector3 pos = transform.position;
        pos.y = 60;
        transform.position = pos;
        bool isOnGround = false;
        int iterations = 0;
        while (isOnGround == false)
        {
            iterations++;
            if (iterations > 100)
            {
                Debug.LogWarning("Ground too far from player.");
                break;
            }
            Block currBlock = chunkManager.GetBlockAtPosition(Vector3Int.RoundToInt(feet.position));
            if (currBlock == null || currBlock.type == null)
            {
                this.transform.position += Vector3.down;
                continue;
            }

            isOnGround = true;
        }

        transform.position += Vector3.up;

        if (playAudio)
        {
            AudioSource.PlayClipAtPoint(this.teleportClip, this.transform.position);
        }
    }

    private void ThrowEnderPearl() {
        GameObject go = Instantiate(enderPearlPrefab, camera.position, Quaternion.identity) as GameObject;
        EnderPearlController pearl = go.GetComponent<EnderPearlController>();
        Vector3 cameraDirection = camera.TransformDirection(Vector3.forward);
        Vector3 direction = Vector3.Lerp(cameraDirection, Vector3.up, 0.1f);
        pearl.Initialize(direction*enderPearlThrowingPower,this);
    }

    void Update()
    {
        _currPosition = new Vector3Int((int)transform.position.x, (int)transform.position.y, (int)transform.position.z);

        if (_currPosition != _prevPosition)
        {
            monitorController.OnBlockTraveled();

            chunkManager.UpdateLoadedChunks();
            _prevPosition = _currPosition;

            // Check if feet in water
            Block currBlock = chunkManager.GetBlockAtPosition(Vector3Int.RoundToInt(feet.position));
            BlockType currentType = currBlock == null ? null : currBlock.type;
            if (_prevType != currentType)
            {
                string blockTypeName = currentType == null ? "Air" : currentType.name;
                if (blockTypeName == "Water")
                {
                    OnEnterWater();
                }
                else if (_prevType != null && _prevType.name == "Water")
                {
                    OnExitWater();
                }

                _prevType = currentType;
            }

            // Check if camera in water
            currBlock = chunkManager.GetBlockAtPosition(Vector3Int.RoundToInt(camera.position));
            currentType = currBlock == null ? null : currBlock.type;
            if (currentType != null && currentType.name == "Water")
            {
                OnCameraEnterWater();
            } else
            {
                OnCameraExitWater();
            }

        }


        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }


        if (Input.GetMouseButtonDown(1))
        {
            PlaceSameBlock();
        }
        else if (Input.GetMouseButtonDown(0))
        {
            if (Cursor.lockState == CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            BreakBlock();
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            LaunchTNT();
        } else if (Input.GetKeyDown(KeyCode.E)) {
            ThrowEnderPearl();
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            this.ToggleUI();
        }


        if (characterController.isGrounded || _isInWater)
        {
            _isJumping = false;
            _jumpYVelocity /= 2.0f;
        }

        if (Input.GetKey(KeyCode.Space) && _isInWater == false && characterController.isGrounded)
        {
            _isJumping = true;
            _jumpTimer = 0.0f;
            _jumpYVelocity = jumpPower;
        }

        Vector3 movement = Vector3.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            movement += this.transform.forward * _speed;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            movement += -this.transform.forward * _speed;
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            movement += -this.transform.right * _speed * sidewaysSpeedModifier;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            movement += this.transform.right * _speed * sidewaysSpeedModifier;
        }

        if (movement.sqrMagnitude > 0.001f)
        {
            PlayStepAudio();
        }

        movement = camera.rotation * movement;

        float mag = movement.magnitude;

        movement.y = 0.0f;
        movement = movement.normalized * mag;


        float gravity = -30f;
        _jumpYVelocity += gravity * Time.deltaTime;

        Vector3 jumpVelocity = Vector3.up * _jumpYVelocity;
        //if (_isInWater)
        //{
        //    jumpVelocity *= waterSpeedModifier;
        //}
        movement += jumpVelocity;

        if (Input.GetKey(KeyCode.Space) && _isInWater)
        {
            movement += Vector3.up * 2f;
            //movement -= Vector3.up * gravity * Time.deltaTime; // Remove effects of gravity
        } else if (_isJumping == false && characterController.isGrounded == false)
        {
            movement += Vector3.up * gravity * Time.deltaTime;
        }

        characterController.Move(movement * Time.deltaTime);


        if (Cursor.lockState == CursorLockMode.Locked)
        {
            DoMouseRotation();
        }
        


        
    }

    private void LateUpdate()
    {
        if (characterController.enabled == false) { // Re-enable character controller after having enough time to teleport
            characterController.enabled = true;
        }
        if (_shouldTeleport) {
            Teleport(_teleportDestination);
            _shouldTeleport = false;
            _teleportDestination = Vector3.zero;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            PlacePlayerOnSurface(true);
        } else if (Input.GetKeyDown(KeyCode.T))
        {
            Teleport();
        }
    }

    private void PlayStepAudio()
    {
        if (stepAudioSource.isPlaying == false)
        {
            Vector3Int positionBeneathFeet = _currPosition + Vector3Int.down;
            Block blockBeneathFeet = chunkManager.GetBlockAtPosition(positionBeneathFeet);
            if (blockBeneathFeet != null && blockBeneathFeet.type != null)
            {
                AudioClip[] stepClips = blockBeneathFeet.type.stepClips;
                if (stepClips != null && stepClips.Length > 0)
                {
                    AudioClip clip = stepClips[Random.Range(0, stepClips.Length - 1)];
                    stepAudioSource.clip = clip;
                    stepAudioSource.Play();
                }
                
            }
        }
    }

    private void SetMaterialColors(Color color)
    {
        foreach(Material material in materialsToColor)
        {
            material.color = color;
        }
    }

    private void OnEnterWater()
    {
        _isInWater = true;
        _speed = baseSpeed * waterSpeedModifier; // Entering water
        GameObject effect = Instantiate(splashEffect, null) as GameObject;
        effect.transform.position = feet.position;
        Destroy(effect, 1.5f);
    }

    private void OnExitWater()
    {
        _isInWater = false;
        _speed = baseSpeed;
        SetMaterialColors(Color.white);
        Camera.main.backgroundColor = initialSkyColor;
    }

    private void OnCameraEnterWater()
    {
        SetMaterialColors(waterTintColor);
        Camera.main.backgroundColor = waterTintColor;

        //RenderSettings.fog = true;
        RenderSettings.fogColor = waterTintColor;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.15f;
    }

    private void OnCameraExitWater()
    {
        SetMaterialColors(Color.white);
        Camera.main.backgroundColor = initialSkyColor;

        //RenderSettings.fog = false;
        RenderSettings.fogColor = initialSkyColor;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 60;
        RenderSettings.fogEndDistance = 65;
    }

    private void DoMouseRotation()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = -Input.GetAxis("Mouse Y");

        rotY += mouseX * mouseSensitivity * Time.deltaTime;
        rotX += mouseY * mouseSensitivity * Time.deltaTime;

        rotX = Mathf.Clamp(rotX, -clampAngle, clampAngle);

        Quaternion localRotation = Quaternion.Euler(rotX, rotY, 0.0f);
        camera.rotation = localRotation;

        body.rotation = camera.rotation;
        Vector3 angles = body.eulerAngles;
        angles.x = 0.0f;
        angles.z = 0.0f;
        body.eulerAngles = angles;
    }

    private void LaunchTNT()
    {
        GameObject prefab = Instantiate(tntPrefab, null);
        prefab.transform.position = camera.position + camera.forward;
        TNTController tnt = prefab.GetComponent<TNTController>();
        tnt.Launch(camera.forward);
    }

    private void ToggleUI()
    {
        foreach(Text text in _textComponents)
        {
            text.gameObject.SetActive(!text.gameObject.activeSelf);
        }
    }

    private void PlaceBlock(string blockTypeName)
    {
        Block block = new Block(blockTypeName);
        PlaceBlock(block);
    }

    private void PlaceBlock(BlockType blockType)
    {
        Block block = new Block(blockType);
        PlaceBlock(block);
    }

    private void PlaceBlock(Block block)
    {
        Vector3Int blockPos;
        if (RaycastToBlock(10.0f, true, out blockPos))
        {
            if (blockPos == _currPosition)
            {
                return; // Don't place block in feet space
            }
            List<Vector3Int> positions = new List<Vector3Int>();
            List<Block> blocks = new List<Block>();

            positions.Add(blockPos);
            blocks.Add(block);

            chunkManager.ModifyBlocks(positions, blocks);

            PlayBlockSound(block.type.name, blockPos);

            monitorController.OnBlockPlaced();
        }
    }

    private Block GetTargetBlock(float maxDistance)
    {
        Vector3Int blockPos;
        if (RaycastToBlock(maxDistance, false, out blockPos))
        {
            Block targetBlock = chunkManager.GetBlockAtPosition(blockPos);
            return targetBlock;
        }

        return null;
    }

    private void PlaceSameBlock()
    {
        Block targetBlock = GetTargetBlock(10.0f);
        if (targetBlock == null)
        {
            return;
        }
        BlockType targetType = targetBlock.type;

        if (targetType.isBillboard)
        {
            return; // Don't place foliage blocks like grass and flowers.
        }

        PlaceBlock(targetType);
    }

    private void BreakBlock()
    {
        Vector3Int blockPos;
        if (RaycastToBlock(10.0f, false, out blockPos))
        {
            List<Vector3Int> positions = new List<Vector3Int>();
            List<Block> blocks = new List<Block>();

            positions.Add(blockPos);
            Block block = new Block("Air");
            blocks.Add(block);

            Block breakingBlock = chunkManager.GetBlockAtPosition(blockPos);
            if (breakingBlock != null && breakingBlock.type != null)
            {
                PlayBlockSound(breakingBlock.type.name, blockPos);


                GameObject effect = Instantiate(breakEffect, null);
                effect.transform.position = blockPos;

                ParticleSystem ps = effect.GetComponent<ParticleSystem>();
                ParticleSystem.MainModule main = ps.main;

                main.startColor = breakingBlock.type.breakParticleColors;

                monitorController.OnBlockDestroyed();

                if (breakingBlock.type.name == "Diamond Ore")
                {
                    monitorController.OnDiamondMined();
                }

            }
            

            chunkManager.ModifyBlocks(positions, blocks);

            
        }
    }

    private void PlayBlockSound(string blockTypeName, Vector3Int position)
    {
        AudioClip clip = BlockType.GetBlockType(blockTypeName).digClip;
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, position);
        }
    }

    private bool RaycastToBlock(in float maxDistance, in bool getEmptyBlock, out Vector3Int hitBlockPosition)
    {
        RaycastHit hit;
        // Does the ray intersect any objects excluding the player layer
        Vector3 direction = camera.TransformDirection(Vector3.forward);
        if (Physics.Raycast(camera.position, direction, out hit, Mathf.Infinity))
        {
            if (hit.distance <= maxDistance)
            {
                Vector3 offset = getEmptyBlock ? direction * -0.01f : direction * 0.01f;
                hitBlockPosition = Vector3Int.RoundToInt(hit.point + offset);
                return true;
            }
        }

        hitBlockPosition = Vector3Int.zero;
        return false;
    }


    //private void OnTriggerEnter(Collider other)
    //{
    //    if (other.CompareTag("Water"))
    //    {
    //        _waterColliders.Add(other);
    //        _speed = baseSpeed * waterSpeedModifier;
    //    }
    //}

    //private void OnTriggerExit(Collider other)
    //{
    //    if (other.CompareTag("Water"))
    //    {
    //        _waterColliders.Remove(other);
    //        if (_waterColliders.Count == 0)
    //        {
    //            _speed = baseSpeed; // Left all water, return to normal speed
    //        }
       
    //    }
    //}
}
